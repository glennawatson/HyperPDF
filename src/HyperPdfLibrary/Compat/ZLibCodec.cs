// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Compresses and decompresses zlib and raw deflate data through the best API each runtime has. On .NET 11 the span-based <c>ZLibDecoder</c>,
/// <c>DeflateDecoder</c> and <c>ZLibEncoder</c> run straight over the input with no stream objects. Each decode leases an
/// idle decoder from its caller's scratch owner until it finishes. On .NET 10 the same calls use <see cref="ZLibStream"/> and <see cref="DeflateStream"/>.
/// </summary>
internal static class ZLibCodec
{
    /// <summary>The usual compression ratio, used to size the first output buffer.</summary>
    private const int ExpectedRatio = 4;

#if NET11_0_OR_GREATER
    /// <summary>The zlib level of <see cref="PdfCompressionLevel.Balanced"/>: zlib's default balance of size and speed.</summary>
    private const int BalancedQuality = 6;

    /// <summary>The zlib level of <see cref="PdfCompressionLevel.Fastest"/>.</summary>
    private const int FastestQuality = 1;

    /// <summary>The zlib level of <see cref="PdfCompressionLevel.Smallest"/>.</summary>
    private const int SmallestQuality = 9;
#endif

    /// <summary>Decompresses with operation-local decoder state.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate.</param>
    /// <param name="output">Receives the decoded bytes.</param>
    /// <returns>Whether the data ended cleanly.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Decompress(ReadOnlySpan<byte> input, bool raw, ref PooledBuffer output) =>
        Decompress(input, raw, ref output, null, CancellationToken.None);

    /// <summary>Decompresses with operation-local state and explicit cancellation.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate.</param>
    /// <param name="output">Receives the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>Whether the data ended cleanly.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Decompress(ReadOnlySpan<byte> input, bool raw, ref PooledBuffer output, CancellationToken cancellationToken) =>
        Decompress(input, raw, ref output, null, cancellationToken);

    /// <summary>Decompresses data, keeping whatever decoded before any damage or truncation.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate rather than zlib.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="scratchPools">The explicit owner, or null for operation-local decoder state.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="true"/> when the data ended cleanly; <see langword="false"/> when it was truncated or damaged and the part before the fault was kept.</returns>
    internal static bool Decompress(ReadOnlySpan<byte> input, bool raw, ref PooledBuffer output, ScratchPools? scratchPools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.IsEmpty)
        {
            return false;
        }

        var hint = (int)Math.Min(PdfLimits.MaxDecodedLength, (long)input.Length * ExpectedRatio);
#if NET11_0_OR_GREATER
        return raw
            ? DecompressRaw(input, ref output, hint, scratchPools?.Objects<DeflateDecoder>(), cancellationToken)
            : DecompressZlib(input, ref output, hint, scratchPools?.Objects<ZLibDecoder>(), cancellationToken);
#else
        return DecompressStream(input, raw, hint, ref output, scratchPools, cancellationToken);
#endif
    }

    /// <summary>Compresses data as zlib.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the compressed bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Compress(ReadOnlySpan<byte> input, ref PooledBuffer output) => Compress(input, PdfCompressionLevel.Balanced, ref output);

    /// <summary>Compresses data as zlib at a level.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="level">The level.</param>
    /// <param name="output">The buffer receiving the compressed bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Compress(ReadOnlySpan<byte> input, PdfCompressionLevel level, ref PooledBuffer output) =>
        Compress(input, level, ref output, null);

    /// <summary>Compresses data with encoder state owned by the document or registered scratch service.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    /// <param name="scratchPools">The explicit owner, or null for operation-local encoder state.</param>
    internal static void Compress(ReadOnlySpan<byte> input, PdfCompressionLevel level, ref PooledBuffer output, ScratchPools? scratchPools)
    {
#if NET11_0_OR_GREATER
        Compress(input, Quality(level), ref output, scratchPools);
#else
        CompressStream(input, level, ref output, scratchPools);
#endif
    }

#if NET11_0_OR_GREATER
    /// <summary>Leases raw decoder state for one operation and returns it to the same owner.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">Receives the decoded bytes.</param>
    /// <param name="hint">The initial output size hint.</param>
    /// <param name="idle">The owner's idle decoders, or null for operation-local state.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>Whether the stream ended cleanly.</returns>
    private static bool DecompressRaw(ReadOnlySpan<byte> input, ref PooledBuffer output, int hint, ScratchObjects<DeflateDecoder>? idle, CancellationToken cancellationToken)
    {
        var decoder = idle?.Take() ?? new();
        try
        {
            decoder.Reset();
            return Decompress(input, ref output, hint, decoder, cancellationToken);
        }
        finally
        {
            if (idle is null || !idle.Offer(decoder))
            {
                decoder.Dispose();
            }
        }
    }

    /// <summary>Leases zlib decoder state for one operation and returns it to the same owner.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">Receives the decoded bytes.</param>
    /// <param name="hint">The initial output size hint.</param>
    /// <param name="idle">The owner's idle decoders, or null for operation-local state.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>Whether the stream ended cleanly.</returns>
    private static bool DecompressZlib(ReadOnlySpan<byte> input, ref PooledBuffer output, int hint, ScratchObjects<ZLibDecoder>? idle, CancellationToken cancellationToken)
    {
        var decoder = idle?.Take() ?? new();
        try
        {
            decoder.Reset();
            return Decompress(input, ref output, hint, decoder, cancellationToken);
        }
        finally
        {
            if (idle is null || !idle.Offer(decoder))
            {
                decoder.Dispose();
            }
        }
    }

    /// <summary>Leases native encoder state from an explicit owner, or creates it for this operation.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="quality">The zlib level.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    /// <param name="scratchPools">The explicit owner, or null for operation-local encoder state.</param>
    private static void Compress(ReadOnlySpan<byte> input, int quality, ref PooledBuffer output, ScratchPools? scratchPools)
    {
        if (scratchPools is null)
        {
            using var encoder = FlateEncoderPool.Create(quality);
            Compress(input, ref output, encoder);
            return;
        }

        var idle = scratchPools.Objects<FlateEncoderPool>();
        var pool = idle.Take() ?? new();
        try
        {
            var encoder = pool.Rent(quality);
            try
            {
                Compress(input, ref output, encoder);
            }
            finally
            {
                pool.Return(quality, encoder);
            }
        }
        finally
        {
            if (!idle.Offer(pool))
            {
                pool.Dispose();
            }
        }
    }

    /// <summary>Writes one complete zlib stream.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    /// <param name="encoder">The exclusively leased encoder.</param>
    private static void Compress(ReadOnlySpan<byte> input, ref PooledBuffer output, ZLibEncoder encoder)
    {
        var max = (int)Math.Min(int.MaxValue, ZLibEncoder.GetMaxCompressedLength(input.Length));
        _ = encoder.Compress(input, output.GetSpan(max), out _, out var written, isFinalBlock: true);
        output.Advance(written);
    }

    /// <summary>Gets the zlib level of a compression level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The zlib level, 1 to 9.</returns>
    private static int Quality(PdfCompressionLevel level) => level switch
    {
        PdfCompressionLevel.Fastest => FastestQuality,
        PdfCompressionLevel.Smallest => SmallestQuality,
        _ => BalancedQuality,
    };

    /// <summary>Decompresses raw deflate with one exclusively leased decoder.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The decoded bytes.</param>
    /// <param name="hint">The initial output size hint.</param>
    /// <param name="decoder">The active decoder.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the data ended cleanly.</returns>
    private static bool Decompress(ReadOnlySpan<byte> input, ref PooledBuffer output, int hint, DeflateDecoder decoder, CancellationToken cancellationToken)
    {
        // At the exact cap the decoder can consume its checksum without output; only another output byte exceeds the limit.
        Span<byte> probe = stackalloc byte[1];
        var status = OperationStatus.DestinationTooSmall;
        while (status == OperationStatus.DestinationTooSmall)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = output.MaximumLength - output.Length;
            var destination = remaining == 0 ? probe : output.GetSpan(Math.Min(hint, remaining));
            status = Step(
                decoder.Decompress(input, destination, out var consumed, out var written),
                consumed,
                written,
                ref input,
                ref output,
                cancellationToken);
            hint = Math.Max(hint, output.Length);
        }

        return status == OperationStatus.Done;
    }

    /// <summary>Decompresses zlib with one exclusively leased decoder.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The decoded bytes.</param>
    /// <param name="hint">The initial output size hint.</param>
    /// <param name="decoder">The active decoder.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the data ended cleanly.</returns>
    private static bool Decompress(ReadOnlySpan<byte> input, ref PooledBuffer output, int hint, ZLibDecoder decoder, CancellationToken cancellationToken)
    {
        // At the exact cap the decoder can consume its checksum without output; only another output byte exceeds the limit.
        Span<byte> probe = stackalloc byte[1];
        var status = OperationStatus.DestinationTooSmall;
        while (status == OperationStatus.DestinationTooSmall)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = output.MaximumLength - output.Length;
            var destination = remaining == 0 ? probe : output.GetSpan(Math.Min(hint, remaining));
            status = Step(
                decoder.Decompress(input, destination, out var consumed, out var written),
                consumed,
                written,
                ref input,
                ref output,
                cancellationToken);
            hint = Math.Max(hint, output.Length);
        }

        return status == OperationStatus.Done;
    }

    /// <summary>Records one decoder step.</summary>
    /// <param name="status">The step's status.</param>
    /// <param name="consumed">The input bytes consumed.</param>
    /// <param name="written">The output bytes written.</param>
    /// <param name="input">The remaining input.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The status; only <see cref="OperationStatus.DestinationTooSmall"/> continues. Done, truncated (NeedMoreData) and damaged (InvalidData) all stop, keeping what decoded.</returns>
    private static OperationStatus Step(OperationStatus status, int consumed, int written, ref ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        output.Advance(written);
        input = input[consumed..];
        return status;
    }
#else
    /// <summary>Compresses through the older stream API with a document-owned output wrapper when available.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    /// <param name="scratchPools">The explicit owner, or null for an operation-local wrapper.</param>
    private static void CompressStream(ReadOnlySpan<byte> input, PdfCompressionLevel level, ref PooledBuffer output, ScratchPools? scratchPools)
    {
        var idle = scratchPools?.Objects<PooledWriteStream>();
        var stream = idle?.Take() ?? new();
        try
        {
            stream.Reset();
            using (var zlib = new ZLibStream(stream, StreamLevel(level), leaveOpen: true))
            {
                zlib.Write(input);
            }

            output.Write(stream.WrittenSpan);
        }
        finally
        {
            stream.Dispose();
            if (idle is not null)
            {
                _ = idle.Offer(stream);
            }
        }
    }

    /// <summary>Gets the stream level of a compression level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The level <see cref="ZLibStream"/> takes.</returns>
    private static CompressionLevel StreamLevel(PdfCompressionLevel level) => level switch
    {
        PdfCompressionLevel.Fastest => CompressionLevel.Fastest,
        PdfCompressionLevel.Smallest => CompressionLevel.SmallestSize,
        _ => CompressionLevel.Optimal,
    };

    /// <summary>Decompresses with <see cref="ZLibStream"/> or <see cref="DeflateStream"/>.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate.</param>
    /// <param name="hint">The first output buffer size.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="scratchPools">The explicit source-wrapper owner, or null for local state.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns><see langword="false"/> when the data was damaged or ended early.</returns>
    private static bool DecompressStream(ReadOnlySpan<byte> input, bool raw, int hint, ref PooledBuffer output, ScratchPools? scratchPools, CancellationToken cancellationToken)
    {
        var idle = scratchPools?.Objects<EndTrackingStream>();
        var source = idle?.Take() ?? new();
        try
        {
            return DecompressStream(input, raw, hint, ref output, source, cancellationToken);
        }
        finally
        {
            if (idle is null || !idle.Offer(source))
            {
                source.Dispose();
            }
        }
    }

    /// <summary>Decodes while the input stays pinned and the wrapper is exclusively leased.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the bytes are raw deflate.</param>
    /// <param name="hint">The initial output size.</param>
    /// <param name="output">Receives decoded bytes.</param>
    /// <param name="source">The exclusively leased input wrapper.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>Whether the compressed input ended cleanly.</returns>
    private static unsafe bool DecompressStream(ReadOnlySpan<byte> input, bool raw, int hint, ref PooledBuffer output, EndTrackingStream source, CancellationToken cancellationToken)
    {
        fixed (byte* pointer = input)
        {
            source.Reset(pointer, input.Length);
            try
            {
                return DecompressStream(raw, hint, ref output, source, cancellationToken);
            }
            finally
            {
                source.Release();
            }
        }
    }

    /// <summary>Reads one exclusively leased stream with a bounded output buffer.</summary>
    /// <param name="raw">Whether to decode raw deflate.</param>
    /// <param name="hint">The initial output size.</param>
    /// <param name="output">Receives decoded bytes.</param>
    /// <param name="source">The attached input wrapper.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>Whether the input ended cleanly.</returns>
    private static bool DecompressStream(bool raw, int hint, ref PooledBuffer output, EndTrackingStream source, CancellationToken cancellationToken)
    {
        using Stream inflater = raw ? new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true) : new ZLibStream(source, CompressionMode.Decompress, leaveOpen: true);

        // GetSpan stays outside the catch: its decode-size cap error must reach the caller, as on .NET 11.
        var clean = true;

        // A one-byte read distinguishes an exact-length stream from an expansion beyond the configured cap.
        Span<byte> probe = stackalloc byte[1];
        var read = 1;
        while (read > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = output.MaximumLength - output.Length;
            var destination = remaining == 0 ? probe : output.GetSpan(Math.Min(hint, remaining));
            read = ReadSome(inflater, destination, ref clean);
            output.Advance(read);
        }

        // The inflater asks for more input only until the final block and checksum are complete.
        return clean && !source.EndReached;
    }

    /// <summary>Reads decoded bytes, treating damaged data as the end of the stream.</summary>
    /// <param name="inflater">The decompressing stream.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="clean">Set to <see langword="false"/> when the data was damaged.</param>
    /// <returns>The bytes read; 0 at the end or at damaged data, keeping what decoded before the damage.</returns>
    private static int ReadSome(Stream inflater, Span<byte> destination, ref bool clean)
    {
        try
        {
            return inflater.Read(destination);
        }
        catch (InvalidDataException)
        {
            clean = false;
            return 0;
        }
    }
#endif
}
