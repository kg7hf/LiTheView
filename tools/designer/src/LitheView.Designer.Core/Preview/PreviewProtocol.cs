// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Text;

namespace LitheView.Designer.Core.Preview;

internal static class PreviewProtocol
{
    internal const uint Magic = 0x3157_504c;
    internal const ushort MajorVersion = 1;
    internal const ushort MinorVersion = 0;
    internal const int HeaderBytes = 28;
    internal const int RenderFixedPayloadBytes = 52;
    internal const int InputBytes = 32;
    internal const int ResponseFixedPayloadBytes = 48;
    internal const int HelloPayloadBytes = 96;
    internal const int MaximumMappingNameBytes = 128;
    internal const int MaximumControlPayloadBytes = PreviewLimits.MaximumSnapshotBytes + 4_096;
    internal const int MaximumRecordBytes = HeaderBytes + MaximumControlPayloadBytes;
    internal const uint ResponseBit = 0x8000_0000;
    internal const uint AnimationRunningFlag = 0x0000_0001;
    internal const string MappingNamePrefix = "Local\\LitheView.Preview.";
    internal const uint CapabilityInlinePixels = 0x0000_0001;
    internal const uint CapabilityNativePackedSurfaces = 0x0000_0002;
    // The maximum surface at four bytes per pixel, matching the worker's inline frame limit.
    internal const int MaximumInlineFrameBytes = 4096 * 4096 * 4;
    internal const int MaximumInlineRecordBytes = HeaderBytes + ResponseFixedPayloadBytes +
        (PreviewLimits.MaximumDamageRegions * 16) + PreviewLimits.MaximumDiagnosticBytes + MaximumInlineFrameBytes;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal enum MessageType : uint
    {
        Hello = 1,
        Render = 2,
        Shutdown = 3
    }

    internal enum WorkerStatus : uint
    {
        Ok = 0,
        MalformedRecord = 1,
        ProtocolMismatch = 2,
        UnsupportedMessage = 3,
        MappingRejected = 4,
        MappingOpenFailed = 5,
        PreviewFailed = 6,
        InternalError = 7
    }

    internal sealed record Record(
        ushort Major,
        ushort Minor,
        uint MessageType,
        uint Flags,
        ulong RequestId,
        byte[] Payload);

    internal sealed record RenderResponse(
        WorkerStatus WorkerStatus,
        PreviewStatus PreviewStatus,
        uint Flags,
        ulong FrameBytes,
        ulong RenderMicroseconds,
        uint NextWakeupMilliseconds,
        uint AcceptedInputCount,
        uint DiagnosticCode,
        IReadOnlyList<PreviewRect> DamageRegions,
        string Diagnostic,
        byte[] InlinePixels);

    internal static byte[] EncodeHello(ulong requestId) =>
        EncodeRecord(MessageType.Hello, flags: 0, requestId, []);

    internal static byte[] EncodeShutdown(ulong requestId) =>
        EncodeRecord(MessageType.Shutdown, flags: 0, requestId, []);

    // A null mapping name asks a worker with the inline-pixels capability to return the frame
    // after its render response.
    internal static byte[] EncodeRender(
        ulong requestId,
        PreviewRenderRequest request,
        string? mappingName,
        int pixelCapacity)
    {
        ArgumentNullException.ThrowIfNull(request);
        byte[] encodedName = [];
        if (mappingName is not null)
        {
            if (!mappingName.StartsWith(MappingNamePrefix, StringComparison.Ordinal) ||
                mappingName.IndexOfAny(['/', ':']) >= 0)
            {
                throw new ArgumentException("The mapping name is outside the preview namespace.", nameof(mappingName));
            }
            encodedName = StrictUtf8.GetBytes(mappingName);
            if (encodedName.Length == 0 || encodedName.Length > MaximumMappingNameBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(mappingName), "The mapping name exceeds the protocol limit.");
            }
        }
        else if (pixelCapacity > MaximumInlineFrameBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelCapacity), "The frame exceeds the inline protocol limit.");
        }
        if (request.Snapshot.IsEmpty || request.Snapshot.Length > PreviewLimits.MaximumSnapshotBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The snapshot exceeds the protocol limit.");
        }
        IReadOnlyList<PreviewInput> inputs = request.Inputs ?? [];
        if (inputs.Count > PreviewLimits.MaximumInputs)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The input count exceeds the protocol limit.");
        }
        if (pixelCapacity != request.Surface.RequiredFrameBytes())
        {
            throw new ArgumentException("The shared pixel capacity does not match the surface.", nameof(pixelCapacity));
        }

        int payloadBytes = checked(
            RenderFixedPayloadBytes + encodedName.Length + request.Snapshot.Length +
            (inputs.Count * InputBytes));
        if (payloadBytes > MaximumControlPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The render control payload exceeds its limit.");
        }
        byte[] payload = new byte[payloadBytes];
        Span<byte> destination = payload;
        BinaryPrimitives.WriteUInt32LittleEndian(destination, request.AbiVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], request.SchemaVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], (uint)request.Surface.PixelFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], (uint)request.Surface.Shape);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], (uint)request.Surface.Quantization);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], request.Surface.Width);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], request.Surface.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], checked((uint)request.Surface.StrideBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[32..], checked((uint)inputs.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[36..], checked((uint)encodedName.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[40..], checked((uint)request.Snapshot.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(destination[44..], checked((ulong)pixelCapacity));

        int cursor = RenderFixedPayloadBytes;
        encodedName.CopyTo(destination[cursor..]);
        cursor += encodedName.Length;
        request.Snapshot.Span.CopyTo(destination[cursor..]);
        cursor += request.Snapshot.Length;
        foreach (PreviewInput input in inputs)
        {
            EncodeInput(destination[cursor..(cursor + InputBytes)], input);
            cursor += InputBytes;
        }
        return EncodeRecord(MessageType.Render, (uint)request.RenderMode, requestId, payload);
    }

    internal static byte[] EncodeRecord(
        MessageType type,
        uint flags,
        ulong requestId,
        ReadOnlySpan<byte> payload,
        ushort major = MajorVersion,
        ushort minor = MinorVersion)
    {
        if (payload.Length > MaximumControlPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }
        int recordBytes = checked(HeaderBytes + payload.Length);
        byte[] encoded = new byte[checked(sizeof(uint) + recordBytes)];
        Span<byte> destination = encoded;
        BinaryPrimitives.WriteUInt32LittleEndian(destination, checked((uint)recordBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], major);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], minor);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], (uint)type);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], flags);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[20..], requestId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], checked((uint)payload.Length));
        payload.CopyTo(destination[32..]);
        return encoded;
    }

    internal static async ValueTask<Record> ReadRecordAsync(
        Stream source,
        CancellationToken cancellationToken,
        int maximumRecordBytes = MaximumRecordBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] length = new byte[sizeof(uint)];
        await source.ReadExactlyAsync(length, cancellationToken).ConfigureAwait(false);
        uint recordLength = BinaryPrimitives.ReadUInt32LittleEndian(length);
        if (recordLength < HeaderBytes || recordLength > maximumRecordBytes)
        {
            throw new PreviewProtocolException($"Worker record length {recordLength} is outside the protocol bounds.");
        }
        byte[] record = new byte[recordLength];
        await source.ReadExactlyAsync(record, cancellationToken).ConfigureAwait(false);
        return DecodeRecord(record);
    }

    internal static Record DecodeRecord(ReadOnlySpan<byte> record)
    {
        if (record.Length < HeaderBytes)
        {
            throw new PreviewProtocolException("Worker record is shorter than its header.");
        }
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(record);
        if (magic != Magic)
        {
            throw new PreviewProtocolException("Worker record magic is invalid.");
        }
        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(record[4..]);
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(record[6..]);
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(record[8..]);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        ulong requestId = BinaryPrimitives.ReadUInt64LittleEndian(record[16..]);
        uint payloadBytes = BinaryPrimitives.ReadUInt32LittleEndian(record[24..]);
        if (payloadBytes != record.Length - HeaderBytes)
        {
            throw new PreviewProtocolException("Worker payload length does not match its record.");
        }
        return new Record(major, minor, type, flags, requestId, record[HeaderBytes..].ToArray());
    }

    internal static PreviewWorkerCapabilities DecodeHello(Record record, ulong expectedRequestId)
    {
        ValidateResponse(record, MessageType.Hello, expectedRequestId, HelloPayloadBytes);
        ReadOnlySpan<byte> payload = record.Payload;
        WorkerStatus status = (WorkerStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (status != WorkerStatus.Ok)
        {
            throw new PreviewProtocolException($"Worker rejected hello with status {status}.");
        }
        return new PreviewWorkerCapabilities(
            BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[24..]),
            DecodeCatalogHash(payload[32..96]))
        {
            InlinePixels = (BinaryPrimitives.ReadUInt32LittleEndian(payload[28..]) & CapabilityInlinePixels) != 0,
            NativePackedSurfaces =
                (BinaryPrimitives.ReadUInt32LittleEndian(payload[28..]) & CapabilityNativePackedSurfaces) != 0
        };
    }

    // inlineFrameBytes is the frame a successful inline render must carry; zero for shared memory.
    internal static RenderResponse DecodeRenderResponse(Record record, ulong expectedRequestId, int inlineFrameBytes = 0)
    {
        ValidateResponse(record, MessageType.Render, expectedRequestId, minimumPayloadBytes: ResponseFixedPayloadBytes);
        ReadOnlySpan<byte> payload = record.Payload;
        WorkerStatus workerStatus = (WorkerStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload);
        PreviewStatus previewStatus = (PreviewStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]);
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]);
        uint damageCount = BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]);
        ulong frameBytes = BinaryPrimitives.ReadUInt64LittleEndian(payload[16..]);
        ulong renderMicroseconds = BinaryPrimitives.ReadUInt64LittleEndian(payload[24..]);
        uint nextWakeup = BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]);
        uint acceptedInputs = BinaryPrimitives.ReadUInt32LittleEndian(payload[36..]);
        uint diagnosticBytes = BinaryPrimitives.ReadUInt32LittleEndian(payload[40..]);
        uint diagnosticCode = BinaryPrimitives.ReadUInt32LittleEndian(payload[44..]);
        if (damageCount > PreviewLimits.MaximumDamageRegions ||
            diagnosticBytes > PreviewLimits.MaximumDiagnosticBytes)
        {
            throw new PreviewProtocolException("Worker response exceeds damage or diagnostic bounds.");
        }
        int expectedBytes = checked(
            ResponseFixedPayloadBytes + checked((int)damageCount * 16) + checked((int)diagnosticBytes));
        int frameBytesCarried = inlineFrameBytes > 0 && workerStatus == WorkerStatus.Ok &&
            previewStatus == PreviewStatus.Ok ? inlineFrameBytes : 0;
        if (frameBytesCarried > 0 && frameBytes != checked((ulong)frameBytesCarried))
        {
            throw new PreviewProtocolException(
                $"Worker frame length {frameBytes} does not match {frameBytesCarried}.");
        }
        if (payload.Length != checked(expectedBytes + frameBytesCarried))
        {
            throw new PreviewProtocolException("Worker render response has an inconsistent payload length.");
        }

        PreviewRect[] damage = new PreviewRect[damageCount];
        int cursor = ResponseFixedPayloadBytes;
        for (int index = 0; index < damage.Length; ++index)
        {
            damage[index] = new PreviewRect(
                BinaryPrimitives.ReadInt32LittleEndian(payload[cursor..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[(cursor + 4)..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[(cursor + 8)..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[(cursor + 12)..]));
            cursor += 16;
        }
        string diagnostic;
        try
        {
            diagnostic = StrictUtf8.GetString(payload.Slice(cursor, checked((int)diagnosticBytes)));
        }
        catch (DecoderFallbackException exception)
        {
            throw new PreviewProtocolException($"Worker diagnostic is not valid UTF-8: {exception.Message}");
        }
        return new RenderResponse(
            workerStatus,
            previewStatus,
            flags,
            frameBytes,
            renderMicroseconds,
            nextWakeup,
            acceptedInputs,
            diagnosticCode,
            damage,
            diagnostic,
            payload.Slice(expectedBytes, frameBytesCarried).ToArray());
    }

    private static void EncodeInput(Span<byte> destination, PreviewInput input)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)input.Kind);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], input.Value);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], input.X);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], input.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], input.TimestampMilliseconds);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[20..], input.ContactId);
        destination[24..InputBytes].Clear();
    }

    private static string DecodeCatalogHash(ReadOnlySpan<byte> encoded)
    {
        string value;
        try
        {
            value = StrictUtf8.GetString(encoded);
        }
        catch (DecoderFallbackException exception)
        {
            throw new PreviewProtocolException($"Worker catalog hash is not valid UTF-8: {exception.Message}");
        }
        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new PreviewProtocolException("Worker catalog hash is not lowercase SHA-256 text.");
        }
        return value;
    }

    private static void ValidateResponse(
        Record record,
        MessageType requestType,
        ulong expectedRequestId,
        int? exactPayloadBytes = null,
        int? minimumPayloadBytes = null)
    {
        if (record.Major != MajorVersion || record.Minor > MinorVersion)
        {
            throw new PreviewProtocolException(
                $"Worker protocol {record.Major}.{record.Minor} is not compatible with {MajorVersion}.{MinorVersion}.");
        }
        uint expectedType = (uint)requestType | ResponseBit;
        if (record.MessageType != expectedType)
        {
            throw new PreviewProtocolException($"Worker response type {record.MessageType} does not match {expectedType}.");
        }
        if (record.RequestId != expectedRequestId)
        {
            throw new PreviewProtocolException(
                $"Stale worker response {record.RequestId} does not match request {expectedRequestId}.");
        }
        if ((exactPayloadBytes.HasValue && record.Payload.Length != exactPayloadBytes.Value) ||
            (minimumPayloadBytes.HasValue && record.Payload.Length < minimumPayloadBytes.Value))
        {
            throw new PreviewProtocolException("Worker response payload length is invalid for its message type.");
        }
    }
}
