// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Checks every line of the standard Huffman tables B.1 to B.15 against annex B of the JBIG2 standard.</summary>
public sealed class Jbig2StandardTableSpecTests
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The base of the binary number system.</summary>
    private const int Binary = 2;

    /// <summary>The range length of the lower and upper range lines.</summary>
    private const int FullRange = 32;

    /// <summary>The offsets, within a line's range, that are decoded.</summary>
    private static readonly int[] Offsets = [0, 1, 5];

    /// <summary>Every line of every standard table decodes to the value the standard gives, and the out-of-band line to out-of-band.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryStandardLineDecodesAsListed()
    {
        var failures = new List<string>();
        for (var number = 1; number <= Jbig2SpecTables.Count; number++)
        {
            failures.AddRange(CheckTable(number));
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Checks one standard table.</summary>
    /// <param name="number">The table number.</param>
    /// <returns>A description of each mismatch.</returns>
    private static List<string> CheckTable(int number)
    {
        var lines = Jbig2SpecTables.Lines(number).ToArray();
        var codes = AssignCodes(lines);
        var table = Jbig2HuffmanTable.GetStandard(number);
        var failures = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            failures.AddRange(Check(table, lines[i], codes[i], number));
        }

        return failures;
    }

    /// <summary>Assigns prefix codes to lines (T.88 annex B.3).</summary>
    /// <param name="lines">The lines, in the standard's order.</param>
    /// <returns>The code of each line, as bits; empty for a line with prefix length 0.</returns>
    private static string[] AssignCodes(Jbig2SpecLine[] lines)
    {
        var maxLength = lines.Max(static line => line.PrefixLength);
        var counts = new int[maxLength + 1];
        foreach (var line in lines)
        {
            counts[line.PrefixLength]++;
        }

        counts[0] = 0;
        var codes = new string[lines.Length];
        var first = 0;
        for (var length = 1; length <= maxLength; length++)
        {
            first = (first + counts[length - 1]) * Binary;
            var code = first;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].PrefixLength != length)
                {
                    continue;
                }

                codes[i] = Convert.ToString(code, Binary).PadLeft(length, '0');
                code++;
            }
        }

        return codes;
    }

    /// <summary>Decodes a line's code with some offsets and compares the values with the standard's.</summary>
    /// <param name="table">The decoder's table.</param>
    /// <param name="line">The line.</param>
    /// <param name="code">The line's prefix code.</param>
    /// <param name="number">The table number, for messages.</param>
    /// <returns>A description of each mismatch.</returns>
    private static IEnumerable<string> Check(Jbig2HuffmanTable table, Jbig2SpecLine line, string code, int number)
    {
        if (line.Kind == Jbig2SpecLineKind.OutOfBand)
        {
            var reader = new Jbig2Reader(Pack(code));
            if (table.Decode(ref reader, out _) != Jbig2HuffmanResult.OutOfBand)
            {
                yield return $"B.{number} out-of-band line";
            }

            yield break;
        }

        foreach (var offset in Offsets)
        {
            if (line.RangeLength < FullRange && offset >= 1 << line.RangeLength)
            {
                continue;
            }

            var bits = code + Convert.ToString(offset, Binary).PadLeft(line.RangeLength, '0');
            var reader = new Jbig2Reader(Pack(bits));
            var result = table.Decode(ref reader, out var value);
            var expected = line.Kind == Jbig2SpecLineKind.Lower ? line.Low - offset : line.Low + offset;
            if (result != Jbig2HuffmanResult.Value || value != expected)
            {
                yield return $"B.{number} line {line}: offset {offset} gave {result} {value}, expected {expected}";
            }
        }
    }

    /// <summary>Packs bits written as text into bytes, with zero padding after.</summary>
    /// <param name="bits">The bits.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Pack(string bits)
    {
        var padded = new StringBuilder(bits).Append('0', ByteBits * ByteBits).ToString();
        var bytes = new byte[padded.Length / ByteBits];
        for (var i = 0; i < padded.Length; i++)
        {
            if (padded[i] == '1')
            {
                bytes[i / ByteBits] |= (byte)(0x80 >> (i % ByteBits));
            }
        }

        return bytes;
    }
}
