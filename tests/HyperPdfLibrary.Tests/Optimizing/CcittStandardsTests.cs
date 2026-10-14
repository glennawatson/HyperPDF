// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>
/// Checks <see cref="CcittEncoder"/> and <see cref="CcittFaxDecoder"/> against ITU-T Recommendations T.4 (07/2003) and T.6
/// (1988): the run-length code words of tables 2, 3a and 3b, make-up coding of long runs, the line synchronisation layout of
/// 4.2.2 to 4.2.4 and the uncompressed-mode extension codes of table 5.
/// </summary>
public sealed class CcittStandardsTests
{
    /// <summary>The end-of-line code (T.4 4.1.2).</summary>
    private const string Eol = "000000000001";

    /// <summary>The white run of zero that starts a line whose first pixel is black (T.4 4.1.1).</summary>
    private const string WhiteZero = "00110101";

    /// <summary>A black run of one pixel.</summary>
    private const string BlackOne = "010";

    /// <summary>The longest run the tests cover: past the 2560 pixels where make-up codes repeat.</summary>
    private const int LongestRun = 5300;

    /// <summary>The longest run the round-trip test covers; every length in the range is encoded and decoded.</summary>
    private const int LongestRoundTripRun = 2700;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The one-dimensional code of the pattern row 11000111 (black as 0): white 2, black 3, white 3.</summary>
    private const string PatternRowBits = "0111101000";

    /// <summary>The width of the pattern row.</summary>
    private const int PatternWidth = 8;

    /// <summary>The rows in the mixed-scheme sample.</summary>
    private const int MixedRows = 2;

    /// <summary>The K value of the mixed-scheme sample.</summary>
    private const int MixedK = 2;

    /// <summary>The pattern row with black as 0 bits.</summary>
    private const byte PatternRow = 0xC7;

    /// <summary>A row that is all white, with black as 0 bits.</summary>
    private const byte WhiteRow = 0xFF;

    /// <summary>Four white pixels then four black ones, with black as 0 bits.</summary>
    private const byte FourWhiteFourBlack = 0xF0;

    /// <summary>The one-dimensional code of four white pixels then four black ones: white 4 (1011), black 4 (011).</summary>
    private const string FourWhiteFourBlackBits = "1011011";

    /// <summary>The lines of end-of-line code and tag bit that make a return-to-control sequence (T.4 4.2.4).</summary>
    private const int ReturnToControlLines = 6;

    /// <summary>The entrance to uncompressed mode on a one-dimensionally coded line (T.4 table 5), then pixels and an exit code.</summary>
    private const string UncompressedOneDimensional = "00000000111111" + "00000011";

    /// <summary>The entrance to uncompressed mode on a two-dimensionally coded line (T.4 table 5, T.6 table 4), then pixels.</summary>
    private const string UncompressedTwoDimensional = "000000111111";

    /// <summary>The rows decoded when the second of three rows enters uncompressed mode: it is damaged, so it repeats the first.</summary>
    private static readonly byte[] DamagedMiddleRow = [PatternRow, PatternRow, FourWhiteFourBlack];

    /// <summary>The pattern row twice.</summary>
    private static readonly byte[] PatternRows = [PatternRow, PatternRow];

    /// <summary>Both run colours, white first.</summary>
    private static readonly bool[] Colours = [false, true];

    /// <summary>The coding schemes tried on every run: Group 3 one-dimensional and Group 4.</summary>
    private static readonly int[] Schemes = [0, -1];

    /// <summary>The encoder writes the code words the tables give for every run length from 0 past two repeats of the 2560 make-up code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WriteRun_EveryRunLength_GivesTheTableCodes()
    {
        for (var run = 0; run <= LongestRun; run++)
        {
            foreach (var black in Colours)
            {
                await Assert.That(RunBits(run, black)).IsEqualTo(CcittBitStrings.Pad(T4RunCodes.Expected(run, black))).Because($"run {run}, black {black}");
            }
        }
    }

    /// <summary>The decoder reads the code words the tables give as that run, in both colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Decode_EveryRunLength_ReadsTheTableCodes()
    {
        for (var run = 1; run <= LongestRun; run++)
        {
            foreach (var black in Colours)
            {
                // A white run is followed by one black pixel; a black run follows a white run of zero.
                var width = black ? run : run + 1;
                var bits = black ? WhiteZero + T4RunCodes.Expected(run, true) : T4RunCodes.Expected(run, false) + BlackOne;
                var decoded = Decode(CcittBitStrings.ToBytes(bits), new(0, width, 1, false, false, false, false), width, 1);

                await Assert.That(CountPixels(decoded, width, black)).IsEqualTo(run).Because($"run {run}, black {black}");
            }
        }
    }

    /// <summary>Every run from 1 pixel to past a make-up code of 2560 round trips, in both colours, one-dimensionally and as Group 4.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoundTrip_EveryRunLength_BothColours()
    {
        for (var run = 1; run <= LongestRoundTripRun; run++)
        {
            foreach (var black in Colours)
            {
                var width = run + 1;
                var row = SingleRun(run, black);
                foreach (var k in Schemes)
                {
                    var options = new CcittParameters(k, width, 1, false, false, false, true);
                    var decoded = Decode(Encode(row, width, options), options, width, 1);

                    await Assert.That(decoded.AsSpan().SequenceEqual(row)).IsTrue().Because($"run {run}, black {black}, K {k}");
                }
            }
        }
    }

    /// <summary>
    /// T.4 4.2.2 and 4.2.4: each line starts with EOL and a tag bit, 1 before a one-dimensional line and 0 before a
    /// two-dimensional one, and the page ends with six lines of EOL and tag bit 1.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_MixedGroup3WithEol_FollowsTheLineSynchronisationLayout()
    {
        var options = new CcittParameters(MixedK, PatternWidth, MixedRows, false, false, true, true);
        var encoded = Encode(PatternRows, PatternWidth, options);
        var returnToControl = string.Concat(Enumerable.Repeat($"{Eol}1", ReturnToControlLines));

        await Assert.That(CcittBitStrings.ToBits(encoded)).IsEqualTo(CcittBitStrings.Pad($"{Eol}1{PatternRowBits}{Eol}0111{returnToControl}"));
    }

    /// <summary>
    /// The uncompressed-mode extension of T.4 table 5 is not decoded: PDF has no way to say a stream uses it, so it counts as a
    /// damaged row. A Group 3 row that enters it is replaced once the next end-of-line code is found, and the next row decodes.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Decode_UncompressedModeOnGroup3Line_IsADamagedRowThatResynchronises()
    {
        var data = CcittBitStrings.ToBytes($"{Eol}{PatternRowBits}{Eol}{UncompressedOneDimensional}{Eol}{FourWhiteFourBlackBits}");

        var rows = Decode(data, new(0, PatternWidth, DamagedMiddleRow.Length, false, false, true, false), PatternWidth, DamagedMiddleRow.Length);

        await Assert.That(rows).IsEquivalentTo(DamagedMiddleRow);
    }

    /// <summary>Entering uncompressed mode from a Group 4 row stops the decoder without an exception, leaving the rest white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Decode_UncompressedModeInGroup4_StopsAndLeavesRowsWhite()
    {
        var data = CcittBitStrings.ToBytes(UncompressedTwoDimensional);

        var rows = Decode(data, new(-1, PatternWidth, MixedRows, false, false, false, true), PatternWidth, MixedRows);

        await Assert.That(Array.TrueForAll(rows, static b => b == WhiteRow)).IsTrue();
    }

    /// <summary>Gets the bits the encoder writes for a run.</summary>
    /// <param name="run">The run length.</param>
    /// <param name="black">Whether the run is black.</param>
    /// <returns>The bits, padded to a whole number of bytes.</returns>
    private static string RunBits(int run, bool black)
    {
        var bits = default(CcittBitWriter);
        try
        {
            CcittCodeTables.WriteRun(ref bits, run, black);
            bits.AlignToByte();
            return CcittBitStrings.ToBits(bits.WrittenSpan.ToArray());
        }
        finally
        {
            bits.Dispose();
        }
    }

    /// <summary>Makes one row of a run of one colour next to one pixel of the other: white then black, or black then white.</summary>
    /// <param name="run">The run length.</param>
    /// <param name="black">Whether the long run is black.</param>
    /// <returns>The packed row, with white padding bits; 1 bits are white.</returns>
    private static byte[] SingleRun(int run, bool black)
    {
        var width = run + 1;
        var row = new byte[(width + ByteBits - 1) / ByteBits];
        for (var x = 0; x < row.Length * ByteBits; x++)
        {
            // A white run is followed by one black pixel; a black run follows one white pixel. The decoder fills the unused bits with white.
            if (x >= width || (black ? x == 0 : x < run))
            {
                row[x / ByteBits] |= (byte)(1 << (ByteBits - 1 - (x % ByteBits)));
            }
        }

        return row;
    }

    /// <summary>Counts the pixels of one colour in a decoded row.</summary>
    /// <param name="row">The decoded row, where 1 bits are white.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="black">Whether to count black pixels; otherwise white ones.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountPixels(byte[] row, int width, bool black)
    {
        var count = 0;
        for (var x = 0; x < width; x++)
        {
            var white = ((row[x / ByteBits] >> (ByteBits - 1 - (x % ByteBits))) & 1) == 1;
            count += white != black ? 1 : 0;
        }

        return count;
    }

    /// <summary>Encodes rows.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="options">The parameters, whose rows value is the height.</param>
    /// <returns>The encoded data.</returns>
    private static byte[] Encode(byte[] rows, int width, CcittParameters options)
    {
        var output = default(PooledBuffer);
        try
        {
            CcittEncoder.Encode(rows, width, options.Rows, options, ref output, new());
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes rows.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="options">The parameters.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The rows.</param>
    /// <returns>The packed rows.</returns>
    private static byte[] Decode(byte[] data, CcittParameters options, int width, int height)
    {
        var rows = new byte[(width + ByteBits - 1) / ByteBits * height];
        CcittFaxDecoder.Decode(data, options, width, height, rows);
        return rows;
    }
}
