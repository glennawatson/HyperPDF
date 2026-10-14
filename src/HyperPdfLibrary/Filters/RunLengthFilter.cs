// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes and encodes RunLengthDecode data.</summary>
internal static class RunLengthFilter
{
    /// <summary>The length byte that ends the data.</summary>
    private const byte EndOfData = 128;

    /// <summary>The value a repeat length byte is subtracted from.</summary>
    private const int RepeatBase = 257;

    /// <summary>The most bytes one run covers.</summary>
    private const int MaxRun = 128;

    /// <summary>The fewest equal bytes worth a repeat run: two equal bytes cost the same as literal bytes and would split the literal run.</summary>
    private const int MinRepeat = 3;

    /// <summary>
    /// Encodes runs (ISO 32000-2 7.4.5): three or more equal bytes become a repeat run of up to 128, other bytes are copied in
    /// literal runs of up to 128, and the data ends with the length byte 128.
    /// </summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the encoded data.</param>
    internal static void Encode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var index = 0;
        var literalStart = 0;
        while (index < input.Length)
        {
            var run = CountRepeats(input[index..]);
            if (run < MinRepeat)
            {
                index += run;
                continue;
            }

            WriteLiteral(input[literalStart..index], ref output);
            output.WriteByte((byte)(RepeatBase - run));
            output.WriteByte(input[index]);
            index += run;
            literalStart = index;
        }

        WriteLiteral(input[literalStart..], ref output);
        output.WriteByte(EndOfData);
    }

    /// <summary>Decodes literal and repeated runs.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var index = 0;
        while (index < input.Length)
        {
            var length = input[index];
            index++;
            if (length == EndOfData)
            {
                return;
            }

            if (length < EndOfData)
            {
                // A literal run of length + 1 bytes.
                var count = Math.Min(length + 1, input.Length - index);
                output.Write(input.Slice(index, count));
                index += count;
                continue;
            }

            if (index >= input.Length)
            {
                return;
            }

            var repeat = RepeatBase - length;
            output.GetSpan(repeat)[..repeat].Fill(input[index]);
            output.Advance(repeat);
            index++;
        }
    }

    /// <summary>Counts equal bytes at the start of a span, up to the longest run.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The count, from 1 to 128.</returns>
    private static int CountRepeats(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || data[1] != data[0])
        {
            return 1;
        }

        var window = data[..Math.Min(MaxRun, data.Length)];
        var end = window.IndexOfAnyExcept(window[0]);
        return end < 0 ? window.Length : end;
    }

    /// <summary>Writes bytes as literal runs.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="output">The output.</param>
    private static void WriteLiteral(ReadOnlySpan<byte> bytes, ref PooledBuffer output)
    {
        while (!bytes.IsEmpty)
        {
            var count = Math.Min(MaxRun, bytes.Length);
            output.WriteByte((byte)(count - 1));
            output.Write(bytes[..count]);
            bytes = bytes[count..];
        }
    }
}
