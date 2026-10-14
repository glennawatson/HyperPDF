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

    /// <summary>The characters ASCIIHexDecode allows before the end marker: hexadecimal digits and white space.</summary>
    private static readonly SearchValues<byte> HexOrWhitespace = SearchValues.Create("0123456789abcdefABCDEF \t\r\n\f\0"u8);

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
    internal static bool TryDecode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var end = input.IndexOf((byte)'>');
        if (end >= 0)
        {
            input = input[..end];
        }

        var destination = output.GetSpan((input.Length + 1) / DigitsPerByte);

        // Unbroken runs of digits decode with the vectorised converter; white space falls back to the scalar loop.
        if ((input.Length & 1) == 0 && Convert.FromHexString(input, destination, out _, out var written) == OperationStatus.Done)
        {
            output.Advance(written);
            return true;
        }

        output.Advance(PdfStringDecoder.DecodeHex(input, destination));
        return input.IndexOfAnyExcept(HexOrWhitespace) < 0;
    }
}
