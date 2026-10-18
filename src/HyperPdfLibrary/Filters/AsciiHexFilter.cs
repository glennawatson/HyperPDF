// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes and encodes ASCIIHexDecode data (ISO 32000-2 7.4.2).</summary>
internal static class AsciiHexFilter
{
    /// <summary>The hexadecimal digits that make one byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The bytes written on each line of encoded text, which keeps lines under 80 characters.</summary>
    private const int BytesPerLine = 36;

    /// <summary>The encoded bytes processed before checking cancellation.</summary>
    private const int DecodeChunkLength = 4096;

    /// <summary>The bits in one hexadecimal digit.</summary>
    private const int HexBits = 4;

    /// <summary>Encodes bytes as upper-case hexadecimal digits in lines of 72 digits, ending with the '&gt;' end-of-data marker.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the text.</param>
    internal static void Encode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var offset = 0;
        while (offset < input.Length)
        {
            var chunk = input.Slice(offset, Math.Min(BytesPerLine, input.Length - offset));
            _ = Convert.TryToHexString(chunk, output.GetSpan((chunk.Length * DigitsPerByte) + 1), out var written);
            output.Advance(written);
            offset += chunk.Length;
            if (offset < input.Length)
            {
                output.WriteByte((byte)'\n');
            }
        }

        output.WriteByte((byte)'>');
    }

    /// <summary>Decodes hexadecimal digits up to the '&gt;' end marker, ignoring white space.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output) => _ = TryDecode(input, ref output);

    /// <summary>
    /// Decodes hexadecimal digits up to the '&gt;' end marker, ignoring white space, and says whether the data held nothing
    /// else (ISO 32000-2 7.4.2: any other character is an error). Other characters are skipped, so a reader still shows the rest.
    /// </summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <returns><see langword="false"/> when the data held a character that is not a hexadecimal digit or white space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryDecode(ReadOnlySpan<byte> input, ref PooledBuffer output) => TryDecode(input, ref output, CancellationToken.None);

    /// <summary>Decodes hexadecimal chunks, retaining a partial digit across chunk boundaries.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>Whether every character is a hexadecimal digit or white space.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> input, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var end = input.IndexOf((byte)'>');
        if (end >= 0)
        {
            input = input[..end];
        }

        var state = new HexChunkState(PdfCharacters.NotHex, true);
        for (var offset = 0; offset < input.Length; offset += DecodeChunkLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = input.Slice(offset, Math.Min(DecodeChunkLength, input.Length - offset));
            if (state.HighNibble >= 0 || !TryVectorChunk(chunk, ref output))
            {
                state = DecodeScalarChunk(chunk, state, ref output);
            }
        }

        if (state.HighNibble >= 0)
        {
            output.WriteByte((byte)(state.HighNibble << HexBits));
        }

        return state.Clean;
    }

    /// <summary>Decodes an even digit run with the vector converter when its full output fits.</summary>
    /// <param name="input">The encoded chunk.</param>
    /// <param name="output">The decoded output.</param>
    /// <returns>Whether the converter decoded the complete chunk.</returns>
    private static bool TryVectorChunk(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var bytes = input.Length / DigitsPerByte;
        if ((input.Length & 1) != 0 || bytes > output.MaximumLength - output.Length)
        {
            return false;
        }

        if (Convert.FromHexString(input, output.GetSpan(bytes), out _, out var written) != OperationStatus.Done)
        {
            return false;
        }

        output.Advance(written);
        return true;
    }

    /// <summary>Decodes a chunk containing white space, an odd digit, or damaged characters.</summary>
    /// <param name="input">The encoded chunk.</param>
    /// <param name="state">The previous chunk's trailing digit and validity.</param>
    /// <param name="output">The decoded output.</param>
    /// <returns>The trailing digit and combined validity.</returns>
    private static HexChunkState DecodeScalarChunk(ReadOnlySpan<byte> input, HexChunkState state, ref PooledBuffer output)
    {
        var (high, clean) = state;
        foreach (var value in input)
        {
            var digit = PdfCharacters.HexValue(value);
            if (digit < 0)
            {
                clean &= PdfCharacters.IsWhitespace(value);
            }
            else if (high < 0)
            {
                high = digit;
            }
            else
            {
                output.WriteByte((byte)((high << HexBits) | digit));
                high = PdfCharacters.NotHex;
            }
        }

        return new(high, clean);
    }

    /// <summary>The hexadecimal state retained between encoded chunks.</summary>
    /// <param name="HighNibble">The trailing digit, or -1 when the byte is complete.</param>
    /// <param name="Clean">Whether all processed characters obey the filter rules.</param>
    private readonly record struct HexChunkState(int HighNibble, bool Clean);
}
