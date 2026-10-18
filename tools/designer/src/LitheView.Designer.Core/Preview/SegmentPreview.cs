// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using System.Globalization;
using LitheView.Designer.Core.Segments;

namespace LitheView.Designer.Core.Preview;

public sealed record SegmentPreviewFrame(byte[] Segments, byte[] Rgba);

public sealed partial class PreviewWorkerClient
{
    public async ValueTask<SegmentPreviewFrame> RenderSegmentsAsync(SegmentGlass glass, SegmentForm form,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        byte[] payload = SegmentPayload(glass, form);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureStartedLockedAsync(cancellationToken).ConfigureAwait(false);
            if (Capabilities is not { SegmentedDisplays: true }) throw new PreviewWorkerException("Rebuild the native worker for segmented preview.", 0);
            ulong id = NextRequestId();
            using CancellationTokenSource deadline = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            try
            {
                await _process!.StandardInput.BaseStream.WriteAsync(PreviewProtocol.EncodeRecord(PreviewProtocol.MessageType.Segments, 0, id, payload), linked.Token).ConfigureAwait(false);
                await _process.StandardInput.BaseStream.FlushAsync(linked.Token).ConfigureAwait(false);
                var response = await PreviewProtocol.ReadRecordAsync(_process.StandardOutput.BaseStream, linked.Token,
                    28 + 20 + 512 + 1024 * 1024 * 4).ConfigureAwait(false);
                byte[] bytes = response.Payload;
                int pixelBytes = checked(glass.ViewWidth * glass.ViewHeight * 4), bitBytes = (glass.SegmentCount + 7) / 8;
                uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
                if (response.RequestId != id || response.MessageType != (PreviewProtocol.ResponseBit | 4) ||
                    response.Major != PreviewProtocol.MajorVersion || response.Minor > PreviewProtocol.MinorVersion || response.Flags != 0 ||
                    bytes.Length < 20 || Read(0) != 0 || Read(4) != glass.ViewWidth || Read(8) != glass.ViewHeight ||
                    Read(12) != bitBytes || Read(16) != pixelBytes || bytes.Length != 20 + bitBytes + pixelBytes)
                    throw new PreviewProtocolException("Invalid or rejected native segmented preview response.");
                return new(bytes.AsSpan(20, bitBytes).ToArray(), bytes.AsSpan(20 + bitBytes).ToArray());
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or PreviewProtocolException)
            {
                LastFailedRequestId = id;
                await TerminateLockedAsync().ConfigureAwait(false);
                throw new PreviewWorkerException("Segmented preview failed; worker will restart on the next request.", id, e);
            }
        }
        finally { _gate.Release(); }
    }

    // This serializes field descriptors and input values, never encoded segment states or pixels.
    private static byte[] SegmentPayload(SegmentGlass glass, SegmentForm form)
    {
        SegmentDocuments.ValidateForm(form, glass);
        if (glass.ViewWidth > 1024 || glass.ViewHeight > 1024) throw new InvalidDataException("Native segmented preview supports viewports up to 1024 by 1024.");
        using MemoryStream buffer = new();
        using BinaryWriter writer = new(buffer);
        void Word(int value) => writer.Write(checked((uint)value));
        Word(glass.ViewWidth); Word(glass.ViewHeight); Word(glass.SegmentCount); Word(form.Bindings.Length); Word(glass.Shapes.Length);
        foreach (SegmentBinding binding in form.Bindings)
        {
            SegmentIcon? icon = glass.Icons.FirstOrDefault(f => f.Id == binding.Field);
            SegmentGroup? group = glass.Groups.FirstOrDefault(f => f.Id == binding.Field);
            SegmentText? text = glass.Texts.FirstOrDefault(f => f.Id == binding.Field);
            SegmentNumber? number = glass.Numbers.FirstOrDefault(f => f.Id == binding.Field);
            int[][] cells = text?.Cells ?? number?.Cells ?? [];
            SegmentGlyph[] glyphs = text?.Glyphs ?? number?.Glyphs ?? [];
            int[] ids = SegmentDocuments.FieldSegments(glass, binding.Field);
            string preview = binding.Preview ?? (icon is not null ? "false" : text is not null ? "" : "0");
            Word(icon is not null ? 0 : group is not null ? 1 : text is not null ? 2 : 3);
            Word(ids.Length); Word(cells.Length > 0 ? cells[0].Length : 0); Word(group?.Maximum ?? 1); Word(glyphs.Length);
            writer.Write(icon is not null ? (preview == "true" ? 1U : 0U) : text is not null ? 0U : uint.Parse(preview, CultureInfo.InvariantCulture));
            Word(text is not null ? preview.Length : 0);
            foreach (int id in ids) Word(id);
            foreach (SegmentGlyph glyph in glyphs)
            {
                Word(glyph.Character[0]);
                uint mask = 0;
                foreach (int stroke in glyph.Strokes) mask |= 1U << stroke;
                writer.Write(mask);
            }
            if (text is not null) foreach (char character in preview) Word(character);
        }
        foreach (SegmentShape shape in glass.Shapes)
        {
            Word(shape.Id); Word(shape.Polygon.Length);
            foreach (SegmentPoint point in shape.Polygon) { Word(point.X); Word(point.Y); }
        }
        if (buffer.Length > PreviewProtocol.MaximumControlPayloadBytes) throw new InvalidDataException("Segmented preview descriptors exceed protocol capacity.");
        return buffer.ToArray();
    }
}
