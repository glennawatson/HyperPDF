// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Compresses and decompresses zlib and raw deflate data through the best API each runtime has. On .NET 11 the span-based <c>ZLibDecoder</c>,
/// <c>DeflateDecoder</c> and <c>ZLibEncoder</c> run straight over the input with no stream objects, one decoder reused
/// per thread. On .NET 10 the same calls use <see cref="ZLibStream"/> and <see cref="DeflateStream"/>.
/// </summary>
internal static class ZLibCodec
{
    /// <summary>The usual compression ratio, used to size the first output buffer.</summary>
    private const int ExpectedRatio = 4;

#if NET11_0_OR_GREATER
    /// <summary>The compression level used when writing: zlib's default balance of size and speed.</summary>
    private const int WriteQuality = 6;

    /// <summary>The window size used when writing, as a power of two.</summary>
    private const int WriteWindowLog2 = 15;

    /// <summary>The zlib decoder of the current thread, reset between streams.</summary>
    [ThreadStatic]
    private static ZLibDecoder? _zlib;

    /// <summary>The raw deflate decoder of the current thread, for streams written without a zlib header.</summary>
    [ThreadStatic]
    private static DeflateDecoder? _deflate;
#endif

    /// <summary>Decompresses data, keeping whatever decoded before any damage or truncation.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate rather than zlib.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="true"/> when the data ended cleanly; <see langword="false"/> when it was truncated or damaged and the part before the fault was kept.</returns>
    internal static bool Decompress(ReadOnlySpan<byte> input, bool raw, ref PooledBuffer output, CancellationToken cancellationToken = default)
    {
        var hint = (int)Math.Min(PdfLimits.MaxDecodedLength, (long)input.Length * ExpectedRatio);
#if NET11_0_OR_GREATER
        OperationStatus status;
        if (raw)
        {
            var deflate = _deflate ??= new DeflateDecoder();
            deflate.Reset();
            while ((status = Step(
                deflate.Decompress(input, output.GetSpan(hint), out var consumed, out var written),
                consumed,
                written,
                ref input,
                ref output,
                cancellationToken)) == OperationStatus.DestinationTooSmall)
            {
                hint = Math.Max(hint, output.Length);
            }

            return status == OperationStatus.Done;
        }

        var zlib = _zlib ??= new ZLibDecoder();
        zlib.Reset();
        while ((status = Step(
            zlib.Decompress(input, output.GetSpan(hint), out var consumed, out var written),
            consumed,
            written,
            ref input,
            ref output,
            cancellationToken)) == OperationStatus.DestinationTooSmall)
        {
            hint = Math.Max(hint, output.Length);
        }

        return status == OperationStatus.Done;
#else
        return DecompressStream(input, raw, hint, ref output, cancellationToken);
#endif
    }

    /// <summary>Compresses data as zlib.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the compressed bytes.</param>
    internal static void Compress(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
#if NET11_0_OR_GREATER
        using var encoder = new ZLibEncoder(WriteQuality, WriteWindowLog2);
        var max = (int)Math.Min(int.MaxValue, ZLibEncoder.GetMaxCompressedLength(input.Length));
        _ = encoder.Compress(input, output.GetSpan(max), out _, out var written, isFinalBlock: true);
        output.Advance(written);
#else
        using var stream = new MemoryStream();
        using (var zlib = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(input);
        }

        output.Write(stream.GetBuffer().AsSpan(0, (int)stream.Length));
#endif
    }

#if NET11_0_OR_GREATER
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
    /// <summary>Decompresses with <see cref="ZLibStream"/> or <see cref="DeflateStream"/>.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="raw">Whether the data is raw deflate.</param>
    /// <param name="hint">The first output buffer size.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns><see langword="false"/> when the data was damaged or ended early.</returns>
    private static unsafe bool DecompressStream(ReadOnlySpan<byte> input, bool raw, int hint, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        fixed (byte* pointer = input)
        {
            using var source = new EndTrackingStream(pointer, input.Length);
            using Stream inflater = raw ? new DeflateStream(source, CompressionMode.Decompress) : new ZLibStream(source, CompressionMode.Decompress);

            // GetSpan stays outside the catch: its decode-size cap error must reach the caller, as on .NET 11.
            var clean = true;
            for (var read = ReadSome(inflater, output.GetSpan(hint), ref clean); read > 0; read = ReadSome(inflater, output.GetSpan(hint), ref clean))
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Advance(read);
            }

            // The inflater asks its source for more bytes only while it has not finished (final block, plus the checksum for zlib),
            // so running into the end of the input means the stream was cut short. The stream itself just stops reading.
            return clean && !source.EndReached;
        }
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
