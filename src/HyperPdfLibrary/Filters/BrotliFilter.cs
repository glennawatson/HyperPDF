// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Decodes BrotliDecode data (RFC 7932). The filter is a proposed extension to ISO 32000-2, not part of it, so the library
/// reads it but never writes it. Damaged or truncated data keeps whatever decoded before the damage, as Flate does.
/// </summary>
internal static class BrotliFilter
{
    /// <summary>The usual compression ratio, used to size the first output buffer.</summary>
    private const int ExpectedRatio = 4;

    /// <summary>Decodes Brotli data.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output) => Decode(input, ref output, CancellationToken.None);

    /// <summary>Decodes Brotli data with cancellation between output chunks.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        if (input.IsEmpty)
        {
            return;
        }

        var hint = (int)Math.Min(PdfLimits.MaxDecodedLength, (long)input.Length * ExpectedRatio);

        // A mutable local, not a using declaration: the decoder is a struct that creates its native state on first use,
        // and a readonly using variable would lose that state to a defensive copy on every call.
        var decoder = default(BrotliDecoder);
        try
        {
            Span<byte> probe = stackalloc byte[1];
            var status = OperationStatus.DestinationTooSmall;
            while (status == OperationStatus.DestinationTooSmall)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = output.MaximumLength - output.Length;
                var destination = remaining == 0 ? probe : output.GetSpan(Math.Min(hint, remaining));
                status = Step(decoder.Decompress(input, destination, out var consumed, out var written), consumed, written, ref input, ref output);
                hint = Math.Max(hint, output.Length);
            }
        }
        finally
        {
            decoder.Dispose();
        }
    }

    /// <summary>Records one decoder step.</summary>
    /// <param name="status">The step's status.</param>
    /// <param name="consumed">The input bytes consumed.</param>
    /// <param name="written">The output bytes written.</param>
    /// <param name="input">The remaining input.</param>
    /// <param name="output">The output buffer.</param>
    /// <returns>The decoder status; only DestinationTooSmall continues decoding.</returns>
    private static OperationStatus Step(OperationStatus status, int consumed, int written, ref ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        output.Advance(written);
        input = input[consumed..];
        return status;
    }
}
