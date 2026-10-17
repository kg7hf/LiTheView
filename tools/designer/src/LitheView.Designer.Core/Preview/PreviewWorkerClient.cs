// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace LitheView.Designer.Core.Preview;

public sealed class PreviewWorkerClient : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly string _workerPath;
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);
    private Process? _process;
    private ulong _requestId;
    private bool _hasStarted;
    private bool _disposed;

    public PreviewWorkerClient(string workerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerPath);
        _workerPath = Path.GetFullPath(workerPath);
    }

    public bool IsRunning => _process is { HasExited: false };

    public int? WorkerProcessId => _process is { HasExited: false } process ? process.Id : null;

    public uint RestartCount { get; private set; }

    public ulong? LastFailedRequestId { get; private set; }

    public PreviewWorkerCapabilities? Capabilities { get; private set; }

    public async ValueTask<PreviewWorkerCapabilities> StartAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnsureStartedLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<PreviewFrame> RenderAsync(
        PreviewRenderRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        int frameBytes = request.Surface.RequiredFrameBytes();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureStartedLockedAsync(cancellationToken).ConfigureAwait(false);
            Process process = _process!;
            ulong requestId = NextRequestId();
            // Windows shares each frame through a designer-owned named mapping. Other hosts have no
            // named shared memory, so the worker returns the frame inline after its response.
            string? mappingName = OperatingSystem.IsWindows()
                ? PreviewProtocol.MappingNamePrefix +
                  Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                  "." + Guid.NewGuid().ToString("N")
                : null;
            using MemoryMappedFile? mapping = mappingName is null
                ? null
                : MemoryMappedFile.CreateNew(mappingName, frameBytes, MemoryMappedFileAccess.ReadWrite);
            using MemoryMappedViewAccessor? view =
                mapping?.CreateViewAccessor(offset: 0, size: frameBytes, MemoryMappedFileAccess.ReadWrite);
            bool inline = view is null;
            RequireCapabilities(Capabilities, request.Surface, inline, requestId);
            byte[] encoded = PreviewProtocol.EncodeRender(requestId, request, mappingName, frameBytes);
            using CancellationTokenSource timeoutSource = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
            try
            {
                await process.StandardInput.BaseStream.WriteAsync(encoded, linked.Token).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync(linked.Token).ConfigureAwait(false);
                PreviewProtocol.Record record = await PreviewProtocol.ReadRecordAsync(
                    process.StandardOutput.BaseStream,
                    linked.Token,
                    inline ? PreviewProtocol.MaximumInlineRecordBytes : PreviewProtocol.MaximumRecordBytes)
                    .ConfigureAwait(false);
                PreviewProtocol.RenderResponse response =
                    PreviewProtocol.DecodeRenderResponse(record, requestId, inline ? frameBytes : 0);
                if (response.WorkerStatus is not PreviewProtocol.WorkerStatus.Ok and
                    not PreviewProtocol.WorkerStatus.PreviewFailed)
                {
                    throw new PreviewWorkerException(
                        $"Preview worker rejected request {requestId}: {response.Diagnostic}",
                        requestId);
                }
                byte[] pixels = [];
                if (response.PreviewStatus == PreviewStatus.Ok)
                {
                    if (response.FrameBytes != checked((ulong)frameBytes))
                    {
                        throw new PreviewProtocolException(
                            $"Worker frame length {response.FrameBytes} does not match {frameBytes}.");
                    }
                    if (view is null)
                    {
                        pixels = response.InlinePixels;
                    }
                    else
                    {
                        pixels = new byte[frameBytes];
                        view.ReadArray(position: 0, pixels, offset: 0, count: pixels.Length);
                    }
                }
                return new PreviewFrame(
                    requestId,
                    request.Surface,
                    pixels,
                    response.DamageRegions,
                    response.PreviewStatus,
                    response.DiagnosticCode,
                    response.Diagnostic,
                    (response.Flags & PreviewProtocol.AnimationRunningFlag) != 0,
                    response.NextWakeupMilliseconds,
                    response.AcceptedInputCount,
                    TimeSpan.FromMicroseconds(checked((long)response.RenderMicroseconds)));
            }
            catch (OperationCanceledException exception)
            {
                LastFailedRequestId = requestId;
                await TerminateLockedAsync().ConfigureAwait(false);
                string reason = cancellationToken.IsCancellationRequested
                    ? "was cancelled"
                    : $"exceeded the {timeout.TotalMilliseconds} ms deadline";
                throw new PreviewWorkerException(
                    $"Preview request {requestId} {reason}; the isolated worker was terminated.",
                    requestId,
                    exception);
            }
            catch (Exception exception) when (
                exception is EndOfStreamException or IOException or PreviewProtocolException or
                    PreviewWorkerException)
            {
                LastFailedRequestId = requestId;
                await TerminateLockedAsync().ConfigureAwait(false);
                if (exception is PreviewWorkerException workerException)
                {
                    throw workerException;
                }
                throw new PreviewWorkerException(
                    $"Preview worker failed request {requestId}; it will be restarted for the next request.",
                    requestId,
                    exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // Requests are never sent to a worker that has not advertised the support they need.
    internal static void RequireCapabilities(
        PreviewWorkerCapabilities? capabilities,
        PreviewSurface surface,
        bool inline,
        ulong requestId)
    {
        if (inline && capabilities is not { InlinePixels: true })
        {
            throw new PreviewWorkerException(
                "This preview worker cannot return frames without shared memory; rebuild it from this checkout.",
                requestId);
        }
        if (surface.IsNativePacked && capabilities is not { NativePackedSurfaces: true })
        {
            throw new PreviewWorkerException(
                "This preview worker cannot render native packed surfaces; rebuild it from this checkout.",
                requestId);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is not { HasExited: false } process)
            {
                DisposeProcess();
                return;
            }
            ulong requestId = NextRequestId();
            try
            {
                byte[] encoded = PreviewProtocol.EncodeShutdown(requestId);
                await process.StandardInput.BaseStream.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                using CancellationTokenSource timeout = new(ShutdownTimeout);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);
                PreviewProtocol.Record response = await PreviewProtocol.ReadRecordAsync(
                    process.StandardOutput.BaseStream,
                    linked.Token).ConfigureAwait(false);
                if (response.RequestId != requestId)
                {
                    throw new PreviewProtocolException("Worker shutdown response is stale.");
                }
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or IOException or PreviewProtocolException)
            {
                await TerminateLockedAsync().ConfigureAwait(false);
                return;
            }
            DisposeProcess();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async ValueTask<PreviewWorkerCapabilities> EnsureStartedLockedAsync(
        CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && Capabilities is not null)
        {
            return Capabilities;
        }
        if (!File.Exists(_workerPath))
        {
            throw new FileNotFoundException("The native preview worker has not been built.", _workerPath);
        }
        DisposeProcess();
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _workerPath,
                Arguments = "--stdio",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        if (!process.Start())
        {
            process.Dispose();
            throw new PreviewWorkerException("The native preview worker did not start.", requestId: 0);
        }
        _process = process;
        if (_hasStarted)
        {
            ++RestartCount;
        }
        _hasStarted = true;
        ulong requestId = NextRequestId();
        using CancellationTokenSource startupTimeout = new(StartupTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            startupTimeout.Token);
        try
        {
            byte[] hello = PreviewProtocol.EncodeHello(requestId);
            await process.StandardInput.BaseStream.WriteAsync(hello, linked.Token).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync(linked.Token).ConfigureAwait(false);
            PreviewProtocol.Record response = await PreviewProtocol.ReadRecordAsync(
                process.StandardOutput.BaseStream,
                linked.Token).ConfigureAwait(false);
            PreviewWorkerCapabilities capabilities = PreviewProtocol.DecodeHello(response, requestId);
            if (capabilities.AbiVersion != PreviewLimits.AbiVersion ||
                capabilities.SchemaVersion != PreviewLimits.SchemaVersion ||
                capabilities.MaximumSnapshotBytes < PreviewLimits.MaximumSnapshotBytes)
            {
                throw new PreviewProtocolException("Worker capabilities do not satisfy the designer contract.");
            }
            Capabilities = capabilities;
            return capabilities;
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or PreviewProtocolException)
        {
            LastFailedRequestId = requestId;
            await TerminateLockedAsync().ConfigureAwait(false);
            throw new PreviewWorkerException(
                "The native preview worker failed version negotiation.",
                requestId,
                exception);
        }
    }

    private ulong NextRequestId()
    {
        _requestId = checked(_requestId + 1);
        return _requestId;
    }

    private async ValueTask TerminateLockedAsync()
    {
        if (_process is { HasExited: false } process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // The worker exited between the state check and termination.
            }
        }
        DisposeProcess();
    }

    private void DisposeProcess()
    {
        _process?.Dispose();
        _process = null;
        Capabilities = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
