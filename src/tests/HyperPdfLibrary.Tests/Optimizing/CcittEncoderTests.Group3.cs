// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Group 3 and option coverage for <see cref="CcittEncoder"/>.</summary>
public sealed partial class CcittEncoderTests
{
    /// <summary>White, white, three black, three white, with black as 0 bits.</summary>
    private const byte PatternRow = 0xC7;

    /// <summary>The width of the pattern row.</summary>
    private const int PatternWidth = 8;

    /// <summary>The bits of the row's one-dimensional code: white 2 (0111), black 3 (10), white 3 (1000).</summary>
    private const string PatternRowBits = "0111101000";

    /// <summary>The bits of the row's Group 4 code against a white row: horizontal mode (001), white 2 (0111), black 3 (10), then V0 (1).</summary>
    private const string GroupFourRowBits = "0010111101";

    /// <summary>The end-of-line code.</summary>
    private const string EolBits = "000000000001";

    /// <summary>Four zero fill bits that make an end-of-line code end on a byte boundary when it starts on one.</summary>
    private const string EolFill = "0000";

    /// <summary>The return-to-control sequence: six end-of-line codes.</summary>
    private const string ReturnToControlBits = EolBits + EolBits + EolBits + EolBits + EolBits + EolBits;

    /// <summary>The end-of-facsimile-block: two end-of-line codes.</summary>
    private const string EndOfBlockBits = EolBits + EolBits;

    /// <summary>The bits of a two-dimensional row that repeats the row above: three vertical-zero codes.</summary>
    private const string RepeatedRowBits = "111";

    /// <summary>The tag bit of a one-dimensional row in mixed Group 3.</summary>
    private const string OneDimensionalTag = "1";

    /// <summary>The tag bit of a two-dimensional row in mixed Group 3.</summary>
    private const string TwoDimensionalTag = "0";

    /// <summary>A one-dimensional row with its tag bit.</summary>
    private const string TaggedOneDimensionalRow = OneDimensionalTag + PatternRowBits;

    /// <summary>A repeated two-dimensional row with its tag bit.</summary>
    private const string TaggedRepeatedRow = TwoDimensionalTag + RepeatedRowBits;

    /// <summary>The rows in each round-trip image, enough for the mixed scheme to repeat its one-dimensional row.</summary>
    private const int MatrixHeight = 9;

    /// <summary>The rows decoded past the image to prove the end of the data stops the decoder.</summary>
    private const int ExtraRows = 2;

    /// <summary>The width of the end-of-block image.</summary>
    private const int EndOfBlockWidth = 64;

    /// <summary>The width of the image with a run beyond the largest make-up code.</summary>
    private const int VeryWideWidth = 6001;

    /// <summary>The rows in the very wide image.</summary>
    private const int VeryWideHeight = 3;

    /// <summary>Two rows.</summary>
    private const int TwoRows = 2;

    /// <summary>The width of the too-short sample.</summary>
    private const int ShortWidth = 16;

    /// <summary>The number base of the bit strings.</summary>
    private const int BinaryRadix = 2;

    /// <summary>The rows in the mixed-scheme sample, where K is 2.</summary>
    private const int MixedHeight = 5;

    /// <summary>The K value of the mixed-scheme sample.</summary>
    private const int MixedK = 2;

    /// <summary>The K value that tests the decoder's stop at the end of block.</summary>
    private const int MixedStopK = 3;

    /// <summary>The bit of an option mask that sets /BlackIs1.</summary>
    private const int BlackIs1Bit = 1;

    /// <summary>The bit of an option mask that sets /EncodedByteAlign.</summary>
    private const int AlignBit = 2;

    /// <summary>The bit of an option mask that sets /EndOfLine.</summary>
    private const int EndOfLineBit = 4;

    /// <summary>The bit of an option mask that sets /EndOfBlock.</summary>
    private const int EndOfBlockBit = 8;

    /// <summary>The number of option masks.</summary>
    private const int OptionMasks = 16;

    /// <summary>One pattern row.</summary>
    private static readonly byte[] OnePatternRow = [PatternRow];

    /// <summary>Two pattern rows.</summary>
    private static readonly byte[] TwoPatternRows = [PatternRow, PatternRow];

    /// <summary>Five pattern rows.</summary>
    private static readonly byte[] FivePatternRows = [PatternRow, PatternRow, PatternRow, PatternRow, PatternRow];

    /// <summary>A single byte, too short for two rows of 16 pixels.</summary>
    private static readonly byte[] ShortRows = [0];

    /// <summary>The widths tried: single pixel, byte edges, the default of 1728 and a run longer than the largest make-up code.</summary>
    private static readonly int[] MatrixWidths = [1, 7, 8, 13, 64, 203, 1728, 2561];

    /// <summary>The patterns tried.</summary>
    private static readonly Pattern[] MatrixPatterns = [Pattern.White, Pattern.Black, Pattern.Checker, Pattern.Sparse, Pattern.Noise, Pattern.Strokes];

    /// <summary>One row coded one-dimensionally gives the code words of T.4 tables 2 and 3 with no end-of-line code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Group3OneDimensional_GivesRunCodes()
    {
        var encoded = EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(0, PatternWidth, false, false, false));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(PatternRowBits));
    }

    /// <summary>An end-of-line code comes before the row when /EndOfLine is set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EndOfLine_PutsEolBeforeTheRow()
    {
        var encoded = EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(0, PatternWidth, false, true, false));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(EolBits + PatternRowBits));
    }

    /// <summary>With /EncodedByteAlign and /EndOfLine, fill bits make the end-of-line code end on a byte boundary, so the row starts on one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EndOfLineAndByteAlign_FillsBeforeTheEol()
    {
        var encoded = EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(0, PatternWidth, true, true, false));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(EolFill + EolBits + PatternRowBits));
    }

    /// <summary>Without end-of-line codes, /EncodedByteAlign pads each row to a byte boundary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_ByteAlignOnly_PadsEveryRow()
    {
        var encoded = EncodeWith(TwoPatternRows, PatternWidth, TwoRows, Scheme(0, PatternWidth, true, false, false));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(PatternRowBits) + Pad(PatternRowBits));
    }

    /// <summary>/EndOfBlock adds the return-to-control sequence of six end-of-line codes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EndOfBlock_AddsSixEndOfLineCodes()
    {
        var encoded = EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(0, PatternWidth, false, false, true));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(PatternRowBits + ReturnToControlBits));
    }

    /// <summary>Group 4 ends with two end-of-line codes, the end-of-facsimile-block.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Group4EndOfBlock_AddsTwoEndOfLineCodes()
    {
        var withBlock = Bits(EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(Group4, PatternWidth, false, false, true)));
        var without = Bits(EncodeWith(OnePatternRow, PatternWidth, 1, Scheme(Group4, PatternWidth, false, false, false)));

        await Assert.That(without).IsEqualTo(Pad(GroupFourRowBits));
        await Assert.That(withBlock).IsEqualTo(Pad(GroupFourRowBits + EndOfBlockBits));
    }

    /// <summary>In mixed Group 3 a tag bit follows the line start: 1 before a one-dimensional row, 0 before a two-dimensional one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Group3Mixed_TagsRowsAsOneOrTwoDimensional()
    {
        var encoded = EncodeWith(TwoPatternRows, PatternWidth, TwoRows, Scheme(MixedK, PatternWidth, false, false, false));

        // Row 0 is one-dimensional. Row 1 repeats row 0, so it is three vertical-zero codes.
        await Assert.That(Bits(encoded)).IsEqualTo(Pad(TaggedOneDimensionalRow + TaggedRepeatedRow));
    }

    /// <summary>In mixed Group 3 every K-th row is one-dimensional again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Group3Mixed_RepeatsAOneDimensionalRowEveryKRows()
    {
        var encoded = EncodeWith(FivePatternRows, PatternWidth, MixedHeight, Scheme(MixedK, PatternWidth, false, false, false));

        await Assert.That(Bits(encoded)).IsEqualTo(Pad(TaggedOneDimensionalRow + TaggedRepeatedRow + TaggedOneDimensionalRow + TaggedRepeatedRow + TaggedOneDimensionalRow));
    }

    /// <summary>Every scheme and option combination decodes back to the same bits, for every width and pattern.</summary>
    /// <param name="k">The /K value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(7)]
    public async Task Encode_AllOptionCombinations_RoundTripThroughTheDecoder(int k)
    {
        foreach (var width in MatrixWidths)
        {
            foreach (var pattern in MatrixPatterns)
            {
                var rows = Make(width, MatrixHeight, pattern);
                for (var mask = 0; mask < OptionMasks; mask++)
                {
                    var options = FromMask(k, width, mask);
                    var decoded = DecodeRows(EncodeWith(rows, width, MatrixHeight, options), width, MatrixHeight, options);

                    await Assert.That(decoded).IsEquivalentTo(rows).Because($"{width} wide, {pattern}, {options}");
                }
            }
        }
    }

    /// <summary>When the data ends with an end-of-block code the decoder stops there, and rows asked for past it stay white.</summary>
    /// <param name="k">The /K value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(MixedStopK)]
    public async Task Encode_EndOfBlock_StopsTheDecoder(int k)
    {
        var rows = Make(EndOfBlockWidth, MatrixHeight, Pattern.Strokes);
        var options = Scheme(k, EndOfBlockWidth, false, false, true);
        var encoded = EncodeWith(rows, EndOfBlockWidth, MatrixHeight, options);
        var decoded = new byte[EndOfBlockWidth / ByteBits * (MatrixHeight + ExtraRows)];
        CcittFaxDecoder.Decode(encoded, options, EndOfBlockWidth, MatrixHeight + ExtraRows, decoded);

        await Assert.That(decoded.AsSpan(0, rows.Length).SequenceEqual(rows)).IsTrue();
        await Assert.That(decoded.AsSpan(rows.Length).ToArray().All(static b => b == FullByte)).IsTrue();
    }

    /// <summary>A wider-than-default image with a run beyond 2,560 pixels needs repeated make-up codes and still round trips.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_RunBeyondLargestMakeUp_RoundTrips()
    {
        var rows = Make(VeryWideWidth, VeryWideHeight, Pattern.Black);
        var options = Scheme(0, VeryWideWidth, false, true, true);

        await Assert.That(DecodeRows(EncodeWith(rows, VeryWideWidth, VeryWideHeight, options), VeryWideWidth, VeryWideHeight, options)).IsEquivalentTo(rows);
    }

    /// <summary>Samples that do not fit the size are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_RowsTooShort_Throws() =>
        await Assert.That(static () => EncodeWith(ShortRows, ShortWidth, TwoRows, Scheme(0, ShortWidth, false, false, true))).Throws<ArgumentOutOfRangeException>();

    /// <summary>Builds the parameters of a scheme with no inverted polarity.</summary>
    /// <param name="k">The /K value.</param>
    /// <param name="width">The image width, used as /Columns.</param>
    /// <param name="byteAlign">The /EncodedByteAlign value.</param>
    /// <param name="endOfLine">The /EndOfLine value.</param>
    /// <param name="endOfBlock">The /EndOfBlock value.</param>
    /// <returns>The parameters.</returns>
    private static CcittParameters Scheme(int k, int width, bool byteAlign, bool endOfLine, bool endOfBlock) =>
        new(k, width, 0, false, byteAlign, endOfLine, endOfBlock);

    /// <summary>Builds the parameters for one of the sixteen option combinations.</summary>
    /// <param name="k">The /K value.</param>
    /// <param name="width">The image width.</param>
    /// <param name="mask">Which options are set.</param>
    /// <returns>The parameters. Group 4 never has end-of-line codes, so that bit is ignored for it.</returns>
    private static CcittParameters FromMask(int k, int width, int mask) =>
        new(k, width, 0, (mask & BlackIs1Bit) != 0, (mask & AlignBit) != 0, k >= 0 && (mask & EndOfLineBit) != 0, (mask & EndOfBlockBit) != 0);

    /// <summary>Encodes rows with parameters.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The encoded data.</returns>
    private static byte[] EncodeWith(byte[] rows, int width, int height, CcittParameters parameters)
    {
        var output = default(PooledBuffer);
        try
        {
            CcittEncoder.Encode(rows, width, height, parameters, ref output, new(), CancellationToken.None);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes rows and clears the padding bits the decoder fills, so they compare with rows made with clear padding.</summary>
    /// <param name="encoded">The encoded data.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="parameters">The parameters the data was encoded with.</param>
    /// <returns>The rows.</returns>
    private static byte[] DecodeRows(byte[] encoded, int width, int height, CcittParameters parameters)
    {
        var stride = (width + ByteBits - 1) / ByteBits;
        var decoded = new byte[stride * height];
        CcittFaxDecoder.Decode(encoded, parameters with { Rows = height }, width, height, decoded);
        var padding = (stride * ByteBits) - width;
        if (padding > 0)
        {
            var mask = (byte)(FullByte << padding);
            for (var y = 0; y < height; y++)
            {
                decoded[(y * stride) + stride - 1] &= mask;
            }
        }

        return decoded;
    }

    /// <summary>Turns bytes into a string of 0 and 1 characters.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The bits, most significant first.</returns>
    private static string Bits(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * ByteBits);
        foreach (var value in bytes)
        {
            _ = text.Append(Convert.ToString(value, BinaryRadix).PadLeft(ByteBits, '0'));
        }

        return text.ToString();
    }

    /// <summary>Pads a bit string with zeros to a whole number of bytes.</summary>
    /// <param name="bits">The bits.</param>
    /// <returns>The padded bits.</returns>
    private static string Pad(string bits) => bits.PadRight((bits.Length + ByteBits - 1) / ByteBits * ByteBits, '0');
}
