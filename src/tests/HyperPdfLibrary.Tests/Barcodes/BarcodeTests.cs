// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Barcodes;

namespace HyperPdfLibrary.Tests.Barcodes;

/// <summary>Checks the shape of each barcode type: its size, its fixed patterns and its limits.</summary>
public sealed class BarcodeTests
{
    /// <summary>The modules in one Code 39 character, three wide elements and six narrow ones.</summary>
    private const int Code39CharacterModules = 15;

    /// <summary>The modules in one Code 128 symbol.</summary>
    private const int Code128SymbolModules = 11;

    /// <summary>The modules in the Code 128 stop symbol.</summary>
    private const int Code128StopModules = 13;

    /// <summary>The modules in a Code 128 barcode of one character: start, character, check and stop.</summary>
    private const int Code128OneCharacterModules = (3 * Code128SymbolModules) + Code128StopModules;

    /// <summary>The modules in an Interleaved 2 of 5 barcode of six digits.</summary>
    private const int Interleaved6Modules = 63;

    /// <summary>The modules in an EAN-13 barcode.</summary>
    private const int Ean13Modules = 95;

    /// <summary>The first module of the EAN-13 centre guard.</summary>
    private const int Ean13CentreStart = 45;

    /// <summary>The modules across a version 1 QR Code.</summary>
    private const int QrVersion1Side = 21;

    /// <summary>The modules across a version 2 QR Code.</summary>
    private const int QrVersion2Side = 25;

    /// <summary>The digits a version 1 QR Code holds at the lowest error correction level.</summary>
    private const int QrVersion1Digits = 41;

    /// <summary>The digits the largest QR Code holds at the lowest error correction level.</summary>
    private const int QrMaximumDigits = 7089;

    /// <summary>The width of a finder pattern.</summary>
    private const int FinderWidth = 7;

    /// <summary>The row and column of the QR timing patterns.</summary>
    private const int TimingLine = 6;

    /// <summary>The column of the QR dark module, and the offset of its row from the bottom.</summary>
    private const int DarkModuleOffset = 8;

    /// <summary>The modules across the smallest Data Matrix.</summary>
    private const int DataMatrixSmallest = 10;

    /// <summary>The modules across the largest Data Matrix.</summary>
    private const int DataMatrixLargest = 144;

    /// <summary>The characters the largest Data Matrix holds.</summary>
    private const int DataMatrixMaximumCharacters = 1558;

    /// <summary>The characters in the Code 39 test text.</summary>
    private const int Code39TextLength = 5;

    /// <summary>The start and stop characters of Code 39.</summary>
    private const int Code39StartAndStop = 2;

    /// <summary>The narrow gaps between the characters of the Code 39 test text, and the one after the last.</summary>
    private const int Code39Gaps = 6;

    /// <summary>The modules across the largest QR Code.</summary>
    private const int QrLargestSide = 177;

    /// <summary>The first printable ASCII character.</summary>
    private const char FirstPrintable = ' ';

    /// <summary>The last printable ASCII character.</summary>
    private const char LastPrintable = '~';

    /// <summary>Code 39 draws start, each character with its gap, and stop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Code39HasTheExpectedWidth()
    {
        var barcode = PdfBarcode.CreateCode39("HELLO");

        await Assert.That(barcode.IsLinear).IsTrue();
        await Assert.That(barcode.Columns).IsEqualTo(((Code39TextLength + Code39StartAndStop) * Code39CharacterModules) + Code39Gaps);
        await Assert.That(barcode.IsDark(0, 0)).IsTrue();
        await Assert.That(barcode.IsDark(barcode.Columns - 1, 0)).IsTrue();
    }

    /// <summary>Code 39 refuses a character outside its set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Code39RejectsLowerCase() =>
        await Assert.That(static () => PdfBarcode.CreateCode39("hello")).Throws<ArgumentException>();

    /// <summary>Every printable character is one symbol in Code 128, so each barcode has the same width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Code128HoldsEveryPrintableCharacterInOneSymbol()
    {
        for (var character = FirstPrintable; character <= LastPrintable; character++)
        {
            var barcode = PdfBarcode.CreateCode128(character.ToString());

            await Assert.That(barcode.Columns).IsEqualTo(Code128OneCharacterModules);
        }
    }

    /// <summary>Code 128 packs a long run of digits two to a symbol.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Code128PacksDigitPairs()
    {
        var digits = PdfBarcode.CreateCode128("123456");
        var letters = PdfBarcode.CreateCode128("abcdef");

        await Assert.That(digits.Columns).IsLessThan(letters.Columns);
    }

    /// <summary>Interleaved 2 of 5 adds a leading zero to an odd count of digits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InterleavedPadsAnOddCount()
    {
        var barcode = PdfBarcode.CreateInterleaved2Of5("12345");

        await Assert.That(barcode.Text).IsEqualTo("012345");
        await Assert.That(barcode.Columns).IsEqualTo(Interleaved6Modules);
    }

    /// <summary>An EAN-13 code is 95 modules with a guard at each end and in the middle, and its check digit is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ean13HasGuardsAndACheckDigit()
    {
        var barcode = PdfBarcode.CreateEan13("590123412345");

        await Assert.That(barcode.Text).IsEqualTo("5901234123457");
        await Assert.That(barcode.Columns).IsEqualTo(Ean13Modules);
        await Assert.That(barcode.IsDark(0, 0)).IsTrue();
        await Assert.That(barcode.IsDark(1, 0)).IsFalse();
        await Assert.That(barcode.IsDark(Code39StartAndStop, 0)).IsTrue();
        await Assert.That(barcode.IsDark(Ean13CentreStart, 0)).IsFalse();
        await Assert.That(barcode.IsDark(Ean13CentreStart + 1, 0)).IsTrue();
        await Assert.That(barcode.IsDark(Ean13Modules - 1, 0)).IsTrue();
    }

    /// <summary>An EAN-13 code with the wrong check digit is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ean13RejectsAWrongCheckDigit() =>
        await Assert.That(static () => PdfBarcode.CreateEan13("5901234123458")).Throws<ArgumentException>();

    /// <summary>A short text fits the smallest QR Code, and a text one digit over its capacity moves up a version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QrChoosesTheSmallestVersion()
    {
        var fits = PdfBarcode.CreateQr(new('7', QrVersion1Digits), PdfQrErrorCorrection.Low);
        var over = PdfBarcode.CreateQr(new('7', QrVersion1Digits + 1), PdfQrErrorCorrection.Low);

        await Assert.That(fits.Columns).IsEqualTo(QrVersion1Side);
        await Assert.That(over.Columns).IsEqualTo(QrVersion2Side);
    }

    /// <summary>A QR Code has the three finder patterns, the timing patterns and the always-dark module.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QrHasItsFixedPatterns()
    {
        var barcode = PdfBarcode.CreateQr("HTTPS://EXAMPLE.COM", PdfQrErrorCorrection.Medium);
        var last = barcode.Columns - 1;

        for (var i = 0; i < FinderWidth; i++)
        {
            await Assert.That(barcode.IsDark(i, 0)).IsTrue();
            await Assert.That(barcode.IsDark(last - i, 0)).IsTrue();
            await Assert.That(barcode.IsDark(i, last)).IsTrue();
        }

        await Assert.That(barcode.IsDark(FinderWidth, 0)).IsFalse();
        await Assert.That(barcode.IsDark(DarkModuleOffset, last - DarkModuleOffset + 1)).IsTrue();
        for (var i = DarkModuleOffset; i < last - DarkModuleOffset; i++)
        {
            await Assert.That(barcode.IsDark(i, TimingLine)).IsEqualTo((i & 1) == 0);
            await Assert.That(barcode.IsDark(TimingLine, i)).IsEqualTo((i & 1) == 0);
        }
    }

    /// <summary>More error correction never makes the symbol smaller.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QrGrowsWithErrorCorrection()
    {
        const string Text = "A fairly long line of text that needs more than the smallest QR Code to hold.";
        var low = PdfBarcode.CreateQr(Text, PdfQrErrorCorrection.Low);
        var high = PdfBarcode.CreateQr(Text, PdfQrErrorCorrection.High);

        await Assert.That(high.Columns).IsGreaterThan(low.Columns);
    }

    /// <summary>The largest QR Code holds 7089 digits and one more is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QrHasAMaximumSize()
    {
        var largest = PdfBarcode.CreateQr(new('1', QrMaximumDigits), PdfQrErrorCorrection.Low);

        await Assert.That(largest.Columns).IsEqualTo(QrLargestSide);
        await Assert.That(static () => PdfBarcode.CreateQr(new('1', QrMaximumDigits + 1), PdfQrErrorCorrection.Low)).Throws<ArgumentException>();
    }

    /// <summary>A short text fits the smallest Data Matrix, which has a solid left and bottom edge and alternating top and right edges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DataMatrixHasItsFinderAndClockEdges()
    {
        var barcode = PdfBarcode.CreateDataMatrix("AB");
        var last = barcode.Columns - 1;

        await Assert.That(barcode.Columns).IsEqualTo(DataMatrixSmallest);
        await Assert.That(barcode.Rows).IsEqualTo(DataMatrixSmallest);
        for (var i = 0; i <= last; i++)
        {
            await Assert.That(barcode.IsDark(0, i)).IsTrue();
            await Assert.That(barcode.IsDark(i, last)).IsTrue();
            await Assert.That(barcode.IsDark(i, 0)).IsEqualTo((i & 1) == 0);
            await Assert.That(barcode.IsDark(last, i)).IsEqualTo((i & 1) == 1 || i == last);
        }
    }

    /// <summary>The largest Data Matrix holds 1558 characters and one more is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DataMatrixHasAMaximumSize()
    {
        var largest = PdfBarcode.CreateDataMatrix(new('x', DataMatrixMaximumCharacters));

        await Assert.That(largest.Columns).IsEqualTo(DataMatrixLargest);
        await Assert.That(static () => PdfBarcode.CreateDataMatrix(new('x', DataMatrixMaximumCharacters + 1))).Throws<ArgumentException>();
    }
}
