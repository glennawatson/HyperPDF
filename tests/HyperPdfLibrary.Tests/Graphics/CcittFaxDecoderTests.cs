// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for the CCITT fax decoder.</summary>
public sealed class CcittFaxDecoderTests
{
    /// <summary>The width of the small test images.</summary>
    private const int SmallWidth = 8;

    /// <summary>The rows decoded from the small test images; the last is past the encoded data.</summary>
    private const int SmallHeight = 3;

    /// <summary>The width of the multi-row Group 4 image.</summary>
    private const int MultiRowWidth = 16;

    /// <summary>The height of the multi-row Group 4 image.</summary>
    private const int MultiRowHeight = 5;

    /// <summary>The width of the long-run test image.</summary>
    private const int WideWidth = 100;

    /// <summary>The first black pixel in the long-run test image.</summary>
    private const int WideBlackStart = 70;

    /// <summary>The K value for Group 4.</summary>
    private const int Group4 = -1;

    /// <summary>The K value for mixed Group 3.</summary>
    private const int Group3TwoDimensional = 1;

    /// <summary>A row that is all white with black as 0 bits.</summary>
    private const byte WhiteRow = 0xFF;

    /// <summary>White, white, three black pixels, three white, with black as 0 bits.</summary>
    private const byte PatternRow = 0xC7;

    /// <summary>The pattern row with black as 1 bits.</summary>
    private const byte PatternRowBlackIs1 = 0x38;

    /// <summary>The row of a damaged image checked to be white.</summary>
    private const int LastRow = 2;

    /// <summary>The end-of-line code.</summary>
    private const string EndOfLineBits = "000000000001";

    /// <summary>The one-dimensional code of the pattern row: white 2, black 3, white 3.</summary>
    private const string PatternBits = "0111101000";

    /// <summary>Eight zeros and a one: no code word starts with eight zeros, and it is too short to pass for an end-of-line code, so the row is damaged.</summary>
    private const string DamagedBits = "000000001";

    /// <summary>The one-dimensional code of four white pixels then four black ones: white 4, black 4.</summary>
    private const string FourWhiteFourBlackBits = "1011011";

    /// <summary>Four white pixels then four black ones, with black as 0 bits.</summary>
    private const byte FourWhiteFourBlack = 0xF0;

    /// <summary>White 29 (00000010) then black 3 (10).</summary>
    private const string WhiteTwentyNineBlackThreeBits = "0000001010";

    /// <summary>Six zero bits that pad ten bits to two bytes.</summary>
    private const string SixFillBits = "000000";

    /// <summary>The width of the row made of a white run of 29 and a black run of 3.</summary>
    private const int WideRowWidth = 32;

    /// <summary>The rows in the wide-row sample.</summary>
    private const int WideRows = 2;

    /// <summary>The rows in the damaged-row sample, counting one damaged row.</summary>
    private const int DamagedRows = 3;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The highest bit index within a byte.</summary>
    private const int HighBit = 7;

    /// <summary>Two rows of 29 white pixels and 3 black ones, with black as 0 bits.</summary>
    private static readonly byte[] WideRowResult = [0xFF, 0xFF, 0xFF, 0xF8, 0xFF, 0xFF, 0xFF, 0xF8];

    /// <summary>The rows decoded when the second of three rows is damaged: the first row twice, then the last row.</summary>
    private static readonly byte[] OneDamagedRowResult = [PatternRow, PatternRow, FourWhiteFourBlack];

    /// <summary>The rows decoded when two rows in a row are damaged: the first row, a copy of it, a white row, then the last row.</summary>
    private static readonly byte[] TwoDamagedRowsResult = [PatternRow, PatternRow, WhiteRow, FourWhiteFourBlack];

    /// <summary>
    /// Group 4: row 1 is V0 (1), all white. Row 2 is horizontal (001), white 2 (0111), black 3 (10), then V0 (1).
    /// Then EOFB (000000000001 twice), padded with zeros.
    /// </summary>
    private static readonly byte[] Group4Pattern = [0x97, 0xA0, 0x02, 0x00, 0x20];

    /// <summary>Group 3 one-dimensional row: white 2 (0111), black 3 (10), white 3 (1000), padded with zeros.</summary>
    private static readonly byte[] Group3Row = [0x7A, 0x00];

    /// <summary>The same row preceded by an end-of-line code (000000000001).</summary>
    private static readonly byte[] Group3RowWithEol = [0x00, 0x17, 0xA0];

    /// <summary>Mixed Group 3: EOL, tag 1, white 8 (10011); EOL, tag 0, H (001), white 2 (0111), black 3 (10), V0 (1).</summary>
    private static readonly byte[] Group3Mixed = [0x00, 0x1C, 0xC0, 0x04, 0x5E, 0x80];

    /// <summary>Group 3 row 100 pixels wide: white make-up 64 (11011), white 6 (1110), black 30 (000001101000).</summary>
    private static readonly byte[] Group3LongRuns = [0xDF, 0x03, 0x40];

    /// <summary>
    /// Group 4, 16 pixels by 5 rows, made by an independent T.6 encoder. It uses horizontal, pass and every vertical
    /// mode: H2,4 H4,2 V0 | VR1 VR1 V0 V0 | V0 VL1 VL2 VR1 | VR2 V0 VL2 VL3 P V0 | P H6,8, then EOFB.
    /// </summary>
    private static readonly byte[] Group4MultiRow = [0x2E, 0xCD, 0xF6, 0xFA, 0x09, 0x87, 0x08, 0x10, 0xC4, 0xF0, 0xA0, 0x02, 0x00, 0x20];

    /// <summary>The rows of <see cref="Group4MultiRow"/>, two bytes each, black as 0 bits.</summary>
    private static readonly byte[] Group4MultiRowExpected = [0xC3, 0xCF, 0xE1, 0xCF, 0xC7, 0xE3, 0x3F, 0xFF, 0xFF, 0x00];

    /// <summary>The first row of <see cref="Group4MultiRow"/> followed by four white rows.</summary>
    private static readonly byte[] Group4FirstRowOnly = [0xC3, 0xCF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    /// <summary>Byte-aligned Group 3 with fill after each EOL: EOL, 4 fill bits, the pattern row, 6 fill bits, twice.</summary>
    private static readonly byte[] Group3AlignedAfterEol = [0x00, 0x10, 0x7A, 0x00, 0x00, 0x10, 0x7A, 0x00];

    /// <summary>Byte-aligned Group 3 with fill before each EOL so it ends on a byte boundary, then the pattern row, twice.</summary>
    private static readonly byte[] Group3AlignedBeforeEol = [0x00, 0x01, 0x7A, 0x00, 0x01, 0x7A, 0x00];

    /// <summary>The pattern row twice then a white row.</summary>
    private static readonly byte[] PatternTwice = [PatternRow, PatternRow, WhiteRow];

    /// <summary>Data that holds no valid code.</summary>
    private static readonly byte[] Garbage = [0x00, 0x00, 0x00, 0x00];

    /// <summary>Three white rows.</summary>
    private static readonly byte[] AllWhite = [WhiteRow, WhiteRow, WhiteRow];

    /// <summary>A white row, the pattern row and a white row.</summary>
    private static readonly byte[] PatternSecond = [WhiteRow, PatternRow, WhiteRow];

    /// <summary>The pattern row then two white rows.</summary>
    private static readonly byte[] PatternFirst = [PatternRow, WhiteRow, WhiteRow];

    /// <summary>A white row, the pattern row and a white row, with black as 1 bits.</summary>
    private static readonly byte[] PatternSecondBlackIs1 = [0, PatternRowBlackIs1, 0];

    /// <summary>Group 4 decodes a white row and a row with a black run, then stops at the end-of-block code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Group4DecodesHandEncodedRows()
    {
        var rows = Decode(Group4Pattern, Parameters(Group4, false, false), SmallWidth, SmallHeight);

        await Assert.That(rows).IsEquivalentTo(PatternSecond);
    }

    /// <summary>BlackIs1 flips the output bits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlackIs1FlipsBits()
    {
        var rows = Decode(Group4Pattern, Parameters(Group4, true, false), SmallWidth, SmallHeight);

        await Assert.That(rows).IsEquivalentTo(PatternSecondBlackIs1);
    }

    /// <summary>A Group 3 one-dimensional row decodes with and without a leading end-of-line code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Group3OneDimensionalDecodes()
    {
        var plain = Decode(Group3Row, Parameters(0, false, false), SmallWidth, SmallHeight);
        var withEol = Decode(Group3RowWithEol, Parameters(0, false, true), SmallWidth, SmallHeight);

        await Assert.That(plain).IsEquivalentTo(PatternFirst);
        await Assert.That(withEol).IsEquivalentTo(PatternFirst);
    }

    /// <summary>Mixed Group 3 decodes a one-dimensional row and a two-dimensional row selected by tag bits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Group3MixedDecodes()
    {
        var rows = Decode(Group3Mixed, Parameters(Group3TwoDimensional, false, true), SmallWidth, SmallHeight);

        await Assert.That(rows).IsEquivalentTo(PatternSecond);
    }

    /// <summary>A multi-row Group 4 image using every two-dimensional mode decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Group4DecodesMultiRowImage()
    {
        var rows = Decode(Group4MultiRow, Parameters(Group4, false, false), MultiRowWidth, MultiRowHeight);

        await Assert.That(rows).IsEquivalentTo(Group4MultiRowExpected);
    }

    /// <summary>A positive /Rows smaller than the image height limits the rows decoded; the rest stay white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowsLimitsDecodedRows()
    {
        var parameters = new CcittParameters(Group4, 0, 1, false, false, false, true);
        var rows = Decode(Group4MultiRow, parameters, MultiRowWidth, MultiRowHeight);

        await Assert.That(rows).IsEquivalentTo(Group4FirstRowOnly);
    }

    /// <summary>Byte-aligned Group 3 decodes with the fill either after or before each end-of-line code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Group3ByteAlignedLayoutsDecode()
    {
        var parameters = new CcittParameters(0, 0, 0, false, true, true, true);
        var after = Decode(Group3AlignedAfterEol, parameters, SmallWidth, SmallHeight);
        var before = Decode(Group3AlignedBeforeEol, parameters, SmallWidth, SmallHeight);

        await Assert.That(after).IsEquivalentTo(PatternTwice);
        await Assert.That(before).IsEquivalentTo(PatternTwice);
    }

    /// <summary>Make-up codes and long black codes decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongRunsDecode()
    {
        var row = Decode(Group3LongRuns, Parameters(0, false, false), WideWidth, 1);
        var firstBlack = -1;
        var blackCount = 0;
        for (var x = 0; x < WideWidth; x++)
        {
            if ((row[x / BitsPerByte] & (1 << (HighBit - (x % BitsPerByte)))) != 0)
            {
                continue;
            }

            firstBlack = firstBlack < 0 ? x : firstBlack;
            blackCount++;
        }

        await Assert.That(firstBlack).IsEqualTo(WideBlackStart);
        await Assert.That(blackCount).IsEqualTo(WideWidth - WideBlackStart);
    }

    /// <summary>Damaged, truncated and empty data do not throw and leave undecoded rows white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataPadsWhite()
    {
        var garbage = Decode(Garbage, Parameters(Group4, false, false), SmallWidth, SmallHeight);
        var truncated = Decode(Group4Pattern.AsSpan(0, 1).ToArray(), Parameters(Group4, false, false), SmallWidth, SmallHeight);
        var empty = Decode([], Parameters(0, false, true), SmallWidth, SmallHeight);

        await Assert.That(garbage).IsEquivalentTo(AllWhite);
        await Assert.That(truncated[0]).IsEqualTo(WhiteRow);
        await Assert.That(truncated[LastRow]).IsEqualTo(WhiteRow);
        await Assert.That(empty).IsEquivalentTo(AllWhite);
    }

    /// <summary>
    /// A Group 3 row that cannot be decoded is replaced by the row before it once its end-of-line code is found again
    /// (ISO 32000-2 table 11, /DamagedRowsBeforeError).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedGroup3RowIsReplacedByThePreviousRow()
    {
        var data = FromBits(EndOfLineBits + PatternBits + EndOfLineBits + DamagedBits + EndOfLineBits + FourWhiteFourBlackBits);

        var rows = Decode(data, new(0, SmallWidth, DamagedRows, false, false, true, false), SmallWidth, DamagedRows);

        await Assert.That(rows).IsEquivalentTo(OneDamagedRowResult);
    }

    /// <summary>A second damaged row in a row becomes white, because the row before it was damaged too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondDamagedGroup3RowInARowIsWhite()
    {
        var data = FromBits(EndOfLineBits + PatternBits + EndOfLineBits + DamagedBits + EndOfLineBits + DamagedBits + EndOfLineBits + FourWhiteFourBlackBits);

        var rows = Decode(data, new(0, SmallWidth, DamagedRows + 1, false, false, true, false), SmallWidth, DamagedRows + 1);

        await Assert.That(rows).IsEquivalentTo(TwoDamagedRowsResult);
    }

    /// <summary>
    /// Byte-aligned Group 3 rows with no end-of-line codes (the TIFF compression 2 layout) decode when the fill zeros and the
    /// leading zeros of the next code word are eleven or more together, which would otherwise pass for an end-of-line code.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ByteAlignedGroup3WithoutEolSkipsFillBeforeLongZeroPrefix()
    {
        // White 29 (00000010, six leading zeros) then black 3 (10), padded with six zeros to a whole number of bytes, twice.
        var data = FromBits(WhiteTwentyNineBlackThreeBits + SixFillBits + WhiteTwentyNineBlackThreeBits + SixFillBits);

        var rows = Decode(data, new(0, WideRowWidth, WideRows, false, true, false, false), WideRowWidth, WideRows);

        await Assert.That(rows).IsEquivalentTo(WideRowResult);
    }

    /// <summary>The parameters read from a dictionary use the specification defaults for missing entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParametersUseDefaults()
    {
        var parms = new PdfDictionary(null);
        parms.Add(KnownName.K, PdfValue.FromInteger(Group4));
        var parameters = CcittParameters.FromDictionary(parms);

        await Assert.That(parameters.K).IsEqualTo(Group4);
        await Assert.That(parameters.Columns).IsEqualTo(CcittParameters.FromDictionary(null).Columns);
        await Assert.That(parameters.EndOfBlock).IsTrue();
        await Assert.That(parameters.BlackIs1).IsFalse();
    }

    /// <summary>Turns a string of 0 and 1 characters into bytes, padding the last byte with zeros.</summary>
    /// <param name="bits">The bits, most significant first.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FromBits(string bits)
    {
        var bytes = new byte[(bits.Length + HighBit) / BitsPerByte];
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] == '1')
            {
                bytes[i / BitsPerByte] |= (byte)(1 << (HighBit - (i % BitsPerByte)));
            }
        }

        return bytes;
    }

    /// <summary>Creates parameters with the columns taken from the image width.</summary>
    /// <param name="k">The coding scheme.</param>
    /// <param name="blackIs1">Whether black is a 1 bit.</param>
    /// <param name="endOfLine">Whether lines start with end-of-line codes.</param>
    /// <returns>The parameters.</returns>
    private static CcittParameters Parameters(int k, bool blackIs1, bool endOfLine) =>
        new(k, 0, 0, blackIs1, false, endOfLine, true);

    /// <summary>Decodes data into a new buffer.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The rows.</param>
    /// <returns>The packed rows.</returns>
    private static byte[] Decode(byte[] data, CcittParameters parameters, int width, int height)
    {
        var output = new byte[(width + HighBit) / BitsPerByte * height];
        CcittFaxDecoder.Decode(data, parameters, width, height, output);
        return output;
    }
}
