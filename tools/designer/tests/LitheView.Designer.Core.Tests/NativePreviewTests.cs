// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using LitheView.Designer.Core.Preview;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Core.Tests;

// Native packed preview: managed contract, capability negotiation and the real worker. The
// worker renders packed storage directly; bytes are compared with NativeStorageOracle.
internal static class NativePreviewTests
{
    // 64 by 48 logical units with an odd-aligned rectangle that splits R2 storage bytes.
    private const string Snapshot =
        "<panel frame=\"0,0,64,48\" backgroundColor=\"0x000000ff\">" +
        "<panel frame=\"8,4,16,8\" backgroundColor=\"0x00000080\"/>" +
        "<panel frame=\"32,16,16,8\" backgroundColor=\"0x0000ffff\"/>" +
        "<panel frame=\"41,33,5,3\" backgroundColor=\"0x00000080\"/>" +
        "</panel>";

    private static readonly NativeStorageOracle.Fill[] Scene =
    [
        new(0, 0, 64, 48, 255, 0),
        new(8, 4, 16, 8, 128, 0),
        new(32, 16, 16, 8, 255, 255),
        new(41, 33, 5, 3, 128, 0)
    ];

    private static readonly (PreviewPixelFormat Format, int Stride)[] PackedFormats =
    [
        (PreviewPixelFormat.R2Packed2x2, 32),
        (PreviewPixelFormat.Rrrr2222, 64),
        (PreviewPixelFormat.Ryry2222, 64)
    ];

    public static void PackedSurfacesDescribeNativeStorage(string repositoryRoot)
    {
        PreviewSurface r2 = new(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 512, 384, 256);
        PreviewSurface cells = new(PreviewPixelFormat.Ryry2222, PreviewShape.Rectangle, PreviewQuantization.Native, 256, 192, 256);
        Require(r2.RequiredFrameBytes() == 49_152 && r2.RowBytes == 256 && cells.RequiredFrameBytes() == 49_152 &&
            (r2 with { StrideBytes = 320 }).RequiredFrameBytes() == 61_440,
            "Packed preview frames occupy block rows times stride.");
        Require(new PreviewSurface(PreviewPixelFormat.Rgb565, PreviewShape.Rectangle, PreviewQuantization.Native, 480, 272, 960)
            .RequiredFrameBytes() == 261_120, "RGB preview frame sizes are unchanged.");
        Throws<ArgumentException>(() => (r2 with { Width = 511 }).RequiredFrameBytes(), "odd R2 width");
        Throws<ArgumentException>(() => (r2 with { Height = 383 }).RequiredFrameBytes(), "odd R2 height");
        Throws<ArgumentException>(() => (r2 with { Quantization = PreviewQuantization.Red2Bpp }).RequiredFrameBytes(),
            "quantized packed surface");
        Throws<ArgumentOutOfRangeException>(() => (r2 with { StrideBytes = 255 }).RequiredFrameBytes(), "short packed stride");

        DisplaySurfaceCatalogLoadResult surfaces = ProfileCatalogLoader.LoadSurfaces(
            Path.Combine(repositoryRoot, "profiles", "display-surfaces"));
        ResolvedDesignerProject Resolve(string fixture)
        {
            string directory = Path.Combine(repositoryRoot, "tests", "designer-fixtures", fixture);
            DesignerProjectResolutionResult resolved = DesignerProjectResolver.Resolve(DesignerProjectLoader.LoadFile(
                Path.Combine(directory, DesignerWorkspaceService.ManifestFileName)), surfaces.Surfaces, directory);
            Require(resolved.IsValid, $"Fixture {fixture} must resolve.");
            return resolved.Project!;
        }
        Require(PreviewSurfaceFactory.Create(Resolve("native-synthetic-r2-smoke").Display) ==
            new PreviewSurface(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 512, 384, 256),
            "A version-3 display previews its own packed storage.");
        foreach (string fixture in new[] { "display-profile-smoke", "round-surface-smoke", "synthetic-red-staging-smoke" })
        {
            ResolvedDesignerProject project = Resolve(fixture);
            Require(PreviewSurfaceFactory.Create(project.Display) == PreviewSurfaceFactory.Create(project.Surface!),
                $"The shared display contract must preview {fixture} exactly as its editing surface did.");
        }
    }

    public static void PackedCapabilityIsNegotiated()
    {
        byte[] hello = new byte[PreviewProtocol.HelloPayloadBytes];
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"u8.CopyTo(hello.AsSpan(32));
        BinaryPrimitives.WriteUInt32LittleEndian(hello.AsSpan(28), PreviewProtocol.CapabilityNativePackedSurfaces);
        byte[] record = PreviewProtocol.EncodeRecord(PreviewProtocol.MessageType.Hello, flags: 0, requestId: 6, hello);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(sizeof(uint) + 8),
            (uint)PreviewProtocol.MessageType.Hello | PreviewProtocol.ResponseBit);
        PreviewWorkerCapabilities packed = PreviewProtocol.DecodeHello(PreviewProtocol.DecodeRecord(record.AsSpan(sizeof(uint))), 6);
        Require(packed.NativePackedSurfaces && !packed.InlinePixels, "The native-packed capability bit was not decoded.");

        PreviewSurface r2 = new(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, 32);
        PreviewSurface rgb = new(PreviewPixelFormat.Rgb565, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, 128);
        PreviewWorkerCapabilities older = packed with { NativePackedSurfaces = false };
        Throws<PreviewWorkerException>(() => PreviewWorkerClient.RequireCapabilities(older, r2, inline: false, 9),
            "a packed request to a worker without the capability");
        Throws<PreviewWorkerException>(() => PreviewWorkerClient.RequireCapabilities(null, r2, inline: false, 9),
            "a packed request before negotiation");
        PreviewWorkerClient.RequireCapabilities(older, rgb, inline: false, 9);
        PreviewWorkerClient.RequireCapabilities(packed, r2, inline: false, 9);
    }

    public static async Task WorkerRendersPackedStorageAsync(string workerPath)
    {
        Require(File.Exists(workerPath), $"Native preview worker is missing: {workerPath}");
        byte[] snapshot = Encoding.UTF8.GetBytes(Snapshot);
        await using PreviewWorkerClient client = new(workerPath);
        PreviewWorkerCapabilities capabilities = await client.StartAsync().ConfigureAwait(false);
        Require(capabilities.NativePackedSurfaces, "This checkout's worker must advertise native packed surfaces.");
        foreach ((PreviewPixelFormat format, int stride) in PackedFormats)
        {
            foreach (int paddedStride in new[] { stride, stride + 3 })
            {
                PreviewSurface surface = new(format, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, paddedStride);
                byte[] expected = NativeStorageOracle.Expected(format, 64, 48, paddedStride, Scene);
                PreviewFrame full = await client.RenderAsync(new PreviewRenderRequest(surface, snapshot),
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                PreviewFrame tiled = await client.RenderAsync(new PreviewRenderRequest(surface, snapshot,
                    RenderMode: PreviewRenderMode.Tiled), TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(full.Status == PreviewStatus.Ok && full.Pixels.AsSpan().SequenceEqual(expected) &&
                    tiled.Status == PreviewStatus.Ok && tiled.Pixels.AsSpan().SequenceEqual(expected),
                    $"{format} worker storage (stride {paddedStride}) must equal the independent oracle: {full.Diagnostic}");
            }
        }

        // Presentation expands emitters only after native rendering: one pixel per emitter level.
        PreviewFrame r2Frame = await client.RenderAsync(new PreviewRenderRequest(
            new(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, 32), snapshot),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        PreviewImage r2Image = PreviewPresentation.ToRgba(r2Frame);
        PreviewFrame yellowFrame = await client.RenderAsync(new PreviewRenderRequest(
            new(PreviewPixelFormat.Ryry2222, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, 64), snapshot),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        PreviewImage yellowImage = PreviewPresentation.ToRgba(yellowFrame);
        Require(r2Image is { Width: 64, Height: 48 } && Pixel(r2Image, 8, 4).SequenceEqual(new byte[] { 170, 0, 0, 255 }) &&
            Pixel(r2Image, 0, 0).SequenceEqual(new byte[] { 255, 0, 0, 255 }) &&
            yellowImage is { Width: 128, Height: 96 } &&
            Pixel(yellowImage, 64, 32).SequenceEqual(new byte[] { 255, 255, 0, 255 }) &&
            Pixel(yellowImage, 65, 32).SequenceEqual(new byte[] { 0, 0, 0, 255 }) &&
            Pixel(yellowImage, 1, 0).SequenceEqual(new byte[] { 255, 0, 0, 255 }),
            "Packed presentation must expand the native emitter levels at emitter resolution.");

        await AssertThrowsAsync<ArgumentException>(() => client.RenderAsync(new PreviewRenderRequest(
            new(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 63, 48, 32), snapshot),
            TimeSpan.FromSeconds(5)).AsTask(), "The client must reject a packed extent without whole storage blocks.");
    }

    // Raw records reach the worker's own fail-closed checks rather than the managed client's.
    public static async Task WorkerRejectsMalformedPackedRequestsAsync(string workerPath)
    {
        Require(File.Exists(workerPath), $"Native preview worker is missing: {workerPath}");
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        Require(process.Start(), "Native preview worker did not start for packed rejection testing.");
        try
        {
            PreviewSurface surface = new(PreviewPixelFormat.R2Packed2x2, PreviewShape.Rectangle, PreviewQuantization.Native, 64, 48, 32);
            int frameBytes = surface.RequiredFrameBytes();
            byte[] snapshot = Encoding.UTF8.GetBytes(Snapshot);

            PreviewProtocol.RenderResponse shortBuffer = await RawRenderAsync(process, surface, snapshot, 31,
                encoded => BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(RenderPayload + 44), (ulong)(frameBytes - 1)))
                .ConfigureAwait(false);
            Require(shortBuffer.PreviewStatus == PreviewStatus.OutputTooSmall && shortBuffer.InlinePixels.Length == 0,
                "The worker must reject a packed frame buffer one byte short of its storage.");
            PreviewProtocol.RenderResponse misaligned = await RawRenderAsync(process, surface, snapshot, 32,
                encoded => BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(RenderPayload + 20), 63)).ConfigureAwait(false);
            Require(misaligned.PreviewStatus == PreviewStatus.InvalidArgument,
                "The worker must reject a packed extent without whole storage blocks.");
            PreviewProtocol.RenderResponse quantized = await RawRenderAsync(process, surface, snapshot, 33,
                encoded => BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(RenderPayload + 16),
                    (uint)PreviewQuantization.Red2Bpp)).ConfigureAwait(false);
            Require(quantized.PreviewStatus == PreviewStatus.UnsupportedFormat,
                "The worker must refuse to quantize a native packed frame.");
            PreviewProtocol.RenderResponse newerMinor = await RawRenderAsync(process, surface, snapshot, 34,
                encoded => BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(sizeof(uint) + 6),
                    (ushort)(PreviewProtocol.MinorVersion + 1))).ConfigureAwait(false);
            PreviewProtocol.RenderResponse newerMajor = await RawRenderAsync(process, surface, snapshot, 35,
                encoded => BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(sizeof(uint) + 4),
                    (ushort)(PreviewProtocol.MajorVersion + 1))).ConfigureAwait(false);
            Require(newerMinor.WorkerStatus == PreviewProtocol.WorkerStatus.ProtocolMismatch &&
                newerMajor.WorkerStatus == PreviewProtocol.WorkerStatus.ProtocolMismatch,
                "The worker must reject records from a newer protocol before rendering.");
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(5_000))
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    // Offset of the render payload inside an encoded record: length prefix plus header.
    private const int RenderPayload = sizeof(uint) + PreviewProtocol.HeaderBytes;

    private static async Task<PreviewProtocol.RenderResponse> RawRenderAsync(Process process, PreviewSurface surface,
        byte[] snapshot, ulong requestId, Action<byte[]> corrupt)
    {
        int frameBytes = surface.RequiredFrameBytes();
        // Windows workers write into a designer-owned mapping; other hosts return pixels inline.
        string? name = OperatingSystem.IsWindows()
            ? PreviewProtocol.MappingNamePrefix + Environment.ProcessId + "." + Guid.NewGuid().ToString("N")
            : null;
        using MemoryMappedFile? mapping = name is null ? null : MemoryMappedFile.CreateNew(name, frameBytes);
        byte[] encoded = PreviewProtocol.EncodeRender(requestId, new PreviewRenderRequest(surface, snapshot), name, frameBytes);
        corrupt(encoded);
        await process.StandardInput.BaseStream.WriteAsync(encoded).ConfigureAwait(false);
        await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
        PreviewProtocol.Record record = await PreviewProtocol.ReadRecordAsync(process.StandardOutput.BaseStream,
            CancellationToken.None, PreviewProtocol.MaximumInlineRecordBytes).ConfigureAwait(false);
        return PreviewProtocol.DecodeRenderResponse(record, requestId);
    }

    private static ReadOnlySpan<byte> Pixel(PreviewImage image, int x, int y) =>
        image.Rgba.AsSpan(((y * image.Width) + x) * 4, 4);

    private static void Throws<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name} for {description}.");
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
