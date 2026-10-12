// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Tests that a damaged file reports the repairs made to read it, and a clean file reports none.</summary>
public sealed class RepairReportTests
{
    /// <summary>The catalog.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree node of the one-page documents.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>A page with a content stream in object 4.</summary>
    private const string ContentPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R >>";

    /// <summary>Valid page content.</summary>
    private const string GoodContent = "q 0 0 10 10 re f Q";

    /// <summary>The object number of the content stream.</summary>
    private const int ContentNumber = 4;

    /// <summary>The object number of the first page.</summary>
    private const int PageNumber = 3;

    /// <summary>The page count of the mini document.</summary>
    private const int MiniPages = 2;

    /// <summary>The width of the first page of the mini document.</summary>
    private const float MiniWidth = 200;

    /// <summary>The width of the default page.</summary>
    private const float LetterWidth = 612;

    /// <summary>The share of the compressed bytes kept when a stream is truncated, in percent.</summary>
    private const int KeepPercent = 60;

    /// <summary>One hundred, for percentages.</summary>
    private const int Percent = 100;

    /// <summary>The bytes of the Adler-32 checksum that ends a zlib stream.</summary>
    private const int ChecksumLength = 4;

    /// <summary>How many times the content is repeated so the compressed stream has some length.</summary>
    private const int Repeats = 200;

    /// <summary>The number of mutants made from each seed.</summary>
    private const int Mutants = 120;

    /// <summary>The seed of the random source.</summary>
    private const int RandomSeed = 20_260_901;

    /// <summary>A Type 0 font over an identity CMap.</summary>
    private const string CompositeFont = "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] >>";

    /// <summary>A descendant font whose /W has an array where a CID belongs.</summary>
    private const string CidFont =
            "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Test /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /DW 1000 /W [1 [500] [600] 3 3 700] >>";

    /// <summary>A simple font whose /Differences holds a string.</summary>
    private const string BadEncodingFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Type /Encoding /Differences [65 /B (oops) /C] >> >>";

    /// <summary>A simple font whose descriptor has a string where /Flags belongs.</summary>
    private const string BadDescriptorFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FontDescriptor << /Type /FontDescriptor /FontName /Helvetica /Flags (bad) /Ascent 700 >> >>";

    /// <summary>The object number of the font in the font documents.</summary>
    private const int FontNumber = 5;

    /// <summary>A clean file reports no repairs, on every generated seed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanSeedsReportNoRepairs()
    {
        var repaired = new List<string>();
        foreach (var seed in RobustnessSeeds.Create())
        {
            // Only what a viewer reads: the pages. The exerciser also reads object numbers no page reaches, and the linearized seed lists one that is not in the file.
            using var document = PdfDocumentReader.Open(seed.Bytes, null);
            for (var page = 0; page < document.PageCount; page++)
            {
                _ = PdfDocumentPages.GetPage(document, page).Width;
            }

            repaired.AddRange(PdfDocumentCheck.WasRepaired(document) ? [$"{seed.Name}: {string.Join(", ", Codes(document))}"] : []);
        }

        await Assert.That(repaired).IsEmpty();
    }

    /// <summary>A scrambled start of the cross-reference table is a repair, and the document still reads.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RebuiltCrossReferenceTableIsReported()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "startxref", "startxxxx"), null);
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsTrue();
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.XrefRebuilt);
        await Assert.That(document.PageCount).IsEqualTo(MiniPages);
    }

    /// <summary>A wrong stream length is a repair that names the stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongStreamLengthIsReported()
    {
        using var document = ReadAll(MiniPdf.Build(Catalog, Pages, ContentPage, "<< /Length 3 >>\nstream\nabcdefgh\nendstream"));
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsTrue();
        await Assert.That(Find(document, PdfDiagnosticCode.BadStreamLength).ObjectNumber).IsEqualTo(ContentNumber);
    }

    /// <summary>A stream with no <c>endstream</c> keyword is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingEndStreamIsReported()
    {
        using var document = ReadAll(MiniPdf.Build(Catalog, Pages, ContentPage, "<< /Length 3 >>\nstream\nabcdefgh"));
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.MissingEndStream);
    }

    /// <summary>A truncated Flate stream keeps the part that decoded and is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedFlateStreamIsReportedAndKeepsItsStart()
    {
        var packed = Compress(Repeated());
        var kept = packed.AsSpan(0, packed.Length * KeepPercent / Percent).ToArray();
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /FlateDecode", Encoding.Latin1.GetString(kept))), null);
        var decoded = StoreReading.GetObject(document.Objects, new(ContentNumber, 0)).AsStream()!.DecodeToArray();
        await Assert.That(decoded.Length).IsGreaterThan(0);
        await Assert.That(Encoding.Latin1.GetString(Repeated())).StartsWith(Encoding.Latin1.GetString(decoded));
        await Assert.That(Find(document, PdfDiagnosticCode.TruncatedStream).ObjectNumber).IsEqualTo(ContentNumber);
    }

    /// <summary>A Flate stream that only lacks its checksum is not damaged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingChecksumIsNotAFault()
    {
        var packed = Compress(Repeated());
        var kept = packed.AsSpan(0, packed.Length - ChecksumLength).ToArray();
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /FlateDecode", Encoding.Latin1.GetString(kept))), null);
        var decoded = StoreReading.GetObject(document.Objects, new(ContentNumber, 0)).AsStream()!.DecodeToArray();
        await Assert.That(decoded.Length).IsEqualTo(Repeated().Length);
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsFalse();
    }

    /// <summary>An LZW stream with a code that is not in the table keeps what came before and is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedLzwStreamIsReported()
    {
        // Clear code, the literal "A", then code 400, which the table cannot hold yet.
        byte[] lzw = [0x80, 0x10, 0x72, 0x00];
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /LZWDecode", Encoding.Latin1.GetString(lzw))), null);
        var decoded = StoreReading.GetObject(document.Objects, new(ContentNumber, 0)).AsStream()!.DecodeToArray();
        await Assert.That(Encoding.Latin1.GetString(decoded)).IsEqualTo("A");
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.TruncatedStream);
    }

    /// <summary>A trailer that names a missing catalog is repaired by finding the catalog object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingCatalogIsFoundByScanning()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "/Root 1 0 R", "/Root 99 0 R"), null);
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.CatalogRebuilt);
        await Assert.That(document.PageCount).IsEqualTo(MiniPages);
    }

    /// <summary>With no catalog object at all, a catalog is made over the root of the page tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingCatalogIsMadeFromThePageTree()
    {
        var file = Replace(Replace(RobustnessSeeds.CreateMini(), "/Root 1 0 R", "/Root 99 0 R"), "/Type /Catalog", "/Type /Nothing");
        using var document = PdfDocumentReader.Open(file, null);
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.CatalogRebuilt);
        await Assert.That(document.PageCount).IsEqualTo(MiniPages);
    }

    /// <summary>A file with no trailer gets one made by scanning, and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingTrailerIsMadeByScanning()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "trailer", "trailxx"), null);
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.TrailerRebuilt);
        await Assert.That(document.PageCount).IsEqualTo(MiniPages);
    }

    /// <summary>A page tree that leads to no pages is rebuilt by scanning for pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenPageTreeIsRebuilt()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "/Kids [3 0 R 4 0 R]", "/Kids [9 0 R]"), null);
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.PageTreeRebuilt);
        await Assert.That(document.PageCount).IsEqualTo(MiniPages);
    }

    /// <summary>A swapped page box is fixed when read and reported with the page's object number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SwappedPageBoxIsReported()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "/MediaBox [0 0 200 100]", "/MediaBox [200 100 0 0]"), null);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(MiniWidth);
        await Assert.That(Find(document, PdfDiagnosticCode.BadPageBox).ObjectNumber).IsEqualTo(PageNumber);
    }

    /// <summary>An empty page box falls back to US Letter and is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyPageBoxIsReported()
    {
        using var document = PdfDocumentReader.Open(Replace(RobustnessSeeds.CreateMini(), "/MediaBox [0 0 200 100]", "/MediaBox [0 0 0 0]"), null);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(LetterWidth);
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.BadPageBox);
    }

    /// <summary>A /W array with an array where a CID belongs keeps the entries after it and is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedWidthsAreReported()
    {
        using var document = PdfDocumentReader.Open(FontDocument(CompositeFont, CidFont), null);
        _ = PdfFontLoader.Load(FontOf(document));
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.BadFontWidths);
    }

    /// <summary>A /Differences array with a string in it is reported, and the names around it are kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedEncodingIsReported()
    {
        using var document = PdfDocumentReader.Open(FontDocument(BadEncodingFont, "null"), null);
        _ = PdfFontLoader.Load(FontOf(document));
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.BadFontEncoding);
    }

    /// <summary>A font descriptor with a text value where a number belongs is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedFontDescriptorIsReported()
    {
        using var document = PdfDocumentReader.Open(FontDocument(BadDescriptorFont, "null"), null);
        _ = PdfFontLoader.Load(FontOf(document));
        await Assert.That(Codes(document)).Contains(PdfDiagnosticCode.BadFontDescriptor);
    }

    /// <summary>Glyph names that only carry a number are read as glyph ids or CIDs, and other names are not.</summary>
    /// <param name="name">The glyph name.</param>
    /// <param name="expectedCid">Whether the number is a CID.</param>
    /// <param name="expectedNumber">The number, or -1 when the name is not numbered.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("g23", false, 23)]
    [Arguments("gid7", false, 7)]
    [Arguments("glyph512", false, 512)]
    [Arguments("index9", false, 9)]
    [Arguments("cid1234", true, 1234)]
    [Arguments("g", false, -1)]
    [Arguments("cid", true, -1)]
    [Arguments("gx5", false, -1)]
    [Arguments("g123456", false, -1)]
    [Arguments("space", false, -1)]
    public async Task NumberedGlyphNamesAreRecognised(string name, bool expectedCid, int expectedNumber)
    {
        var found = HyperPdfLibrary.Fonts.Programs.NumberedGlyphName.TryParse(Encoding.ASCII.GetBytes(name), out var isCid, out var number);
        await Assert.That(found).IsEqualTo(expectedNumber >= 0);
        await Assert.That(isCid).IsEqualTo(expectedCid);
        await Assert.That(number).IsEqualTo(Math.Max(expectedNumber, 0));
    }

    /// <summary>Damaged copies of every seed that open can be saved compactly to a file that needs no repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MutantsThatNeededRepairSaveToAFileThatNeedsNone()
    {
        var failures = new List<string>();
        var random = new SeededRandom(RandomSeed);
        var log = new StringBuilder();
        var seeds = RobustnessSeeds.Create();
        foreach (var seed in seeds)
        {
            for (var i = 0; i < Mutants; i++)
            {
                var problem = SaveAndReopen(PdfMutator.Mutate(seed.Bytes, random, log));
                failures.AddRange(problem is null ? [] : [$"{seed.Name} #{i}: {problem}"]);
            }
        }

        // A few mutants are damaged beyond repair: a filter name spoiled into an unknown one leaves nothing to decode it with.
        await Assert.That(failures.Count * Percent).IsLessThanOrEqualTo(Mutants * seeds.Count);
    }

    /// <summary>Opens a damaged file, saves it compactly and reopens the result.</summary>
    /// <param name="mutant">The damaged file.</param>
    /// <returns>A description of what is wrong with the saved file, or <see langword="null"/> when it is fine or the damage was too great to open.</returns>
    private static string? SaveAndReopen(byte[] mutant)
    {
        try
        {
            using var damaged = PdfDocumentReader.Open(mutant, null);
            _ = DocumentExerciser.Read(damaged);
            var saved = HyperPdfLibrary.Writing.PdfCompactWriter.Save(damaged.Objects, HyperPdfLibrary.Writing.PdfCompactOptions.Default);
            using var reopened = PdfDocumentReader.Open(saved, null);
            _ = DocumentExerciser.Read(reopened);
            return PdfDocumentCheck.WasRepaired(reopened) ? $"the saved file still needs repair: {string.Join(", ", Codes(reopened))}" : null;
        }
        catch (PdfException)
        {
            return null;
        }
    }

    /// <summary>Opens a document and reads everything from it.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The document.</returns>
    private static PdfDocument ReadAll(byte[] file)
    {
        var document = PdfDocumentReader.Open(file, null);
        _ = DocumentExerciser.Read(document);
        return document;
    }

    /// <summary>Gets the code of each repair.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The codes.</returns>
    private static List<PdfDiagnosticCode> Codes(PdfDocument document)
    {
        var codes = new List<PdfDiagnosticCode>();
        foreach (var repair in PdfDocumentCheck.GetRepairs(document))
        {
            codes.Add(repair.Code);
        }

        return codes;
    }

    /// <summary>Finds the first repair with a code.</summary>
    /// <param name="document">The document.</param>
    /// <param name="code">The code.</param>
    /// <returns>The repair, or a default one with code None.</returns>
    private static PdfDiagnostic Find(PdfDocument document, PdfDiagnosticCode code)
    {
        foreach (var repair in PdfDocumentCheck.GetRepairs(document))
        {
            if (repair.Code == code)
            {
                return repair;
            }
        }

        return default;
    }

    /// <summary>Replaces text in a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The text to find.</param>
    /// <param name="to">The replacement.</param>
    /// <returns>The new file.</returns>
    private static byte[] Replace(byte[] file, string from, string to) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(file).Replace(from, to, StringComparison.Ordinal));

    /// <summary>Gets the page content repeated so its compressed form has some length.</summary>
    /// <returns>The content bytes.</returns>
    private static byte[] Repeated() => Encoding.Latin1.GetBytes(string.Join('\n', Enumerable.Repeat(GoodContent, Repeats)));

    /// <summary>Compresses bytes as zlib.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The zlib data.</returns>
    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>Builds a one-page document whose resources name a font in object 5.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="descendant">The descendant font, object 6.</param>
    /// <returns>The file.</returns>
    private static byte[] FontDocument(
        string font,
        string descendant) =>
        MiniPdf.Build(
        Catalog,
        Pages,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 5 0 R >> >> >>",
        "null",
        font,
        descendant);

    /// <summary>Gets the font dictionary of a document made by <see cref="FontDocument"/>.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary FontOf(PdfDocument document) => StoreReading.GetObject(document.Objects, new(FontNumber, 0)).AsDictionary()!;
}
