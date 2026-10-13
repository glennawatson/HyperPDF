// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// Tests that article threads, page-piece data, transitions, document parts and web capture data read in order and
/// stay bounded on malformed files (ISO 32000-2 12.4.3, 12.4.4.1, 14.5, 14.10 and 14.12).
/// </summary>
public sealed class ArticleAndPartReadingTests
{
    /// <summary>The number of beads in the three-bead thread.</summary>
    private const int ThreeBeads = 3;

    /// <summary>The number of beads read before the second broken ring ends.</summary>
    private const int TwoBeads = 2;

    /// <summary>The index of the third item in a list.</summary>
    private const int Third = 2;

    /// <summary>The number of well-formed threads in the wrong-typed sample.</summary>
    private const int TwoThreads = 2;

    /// <summary>The number of children of the wrong-typed root part.</summary>
    private const int TwoChildren = 2;

    /// <summary>The left edge of the first bead.</summary>
    private const float FirstLeft = 158;

    /// <summary>The top edge of the first bead.</summary>
    private const float FirstTop = 905;

    /// <summary>The left edge of the second bead.</summary>
    private const float SecondLeft = 322;

    /// <summary>The left edge of the third bead.</summary>
    private const float ThirdLeft = 157;

    /// <summary>The bottom edge of the third bead.</summary>
    private const float ThirdBottom = 254;

    /// <summary>The object number of the first object after the three pages in the document part samples.</summary>
    private const int FirstPartObject = 6;

    /// <summary>The page index of the third page.</summary>
    private const int ThirdPage = 2;

    /// <summary>The number of top-level parts in the nested sample.</summary>
    private const int TopParts = 2;

    /// <summary>The number of nodes in the deep document part chain, more than the reader follows.</summary>
    private const int DeepChain = 80;

    /// <summary>The most levels the reader follows below the root.</summary>
    private const int MaxPartDepth = 64;

    /// <summary>The duration of the split transition.</summary>
    private const double SplitDuration = 3.5;

    /// <summary>The display duration of the split page.</summary>
    private const double SplitDisplay = 5;

    /// <summary>The wipe direction: top to bottom.</summary>
    private const int TopToBottom = 270;

    /// <summary>The number of content sets in the web capture sample.</summary>
    private const int ContentSets = 3;

    /// <summary>The number of sources of the first content set.</summary>
    private const int Sources = 2;

    /// <summary>The source type of the second source: a POST form submission.</summary>
    private const int PostSource = 2;

    /// <summary>The year of the web capture timestamp.</summary>
    private const int CaptureYear = 2023;

    /// <summary>The number of entries in the sample IDS tree.</summary>
    private const int IdEntries = 3;

    /// <summary>The web capture version in the sample.</summary>
    private const double CaptureVersion = 1.1;

    /// <summary>The document part metadata key and level name for a recipient.</summary>
    private const string RecipientKey = "Recipient";

    /// <summary>The digital identifier of the first content set.</summary>
    private static readonly byte[] FirstSetId = [0x01, 0x02];

    /// <summary>A three-bead thread keeps the /N order from /F, each bead's rectangle and its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreeBeadThreadKeepsOrderAndRectangles()
    {
        // ISO 32000-2 12.4.3 Example 2, with /F naming the middle object so the order comes from /N, not object numbers.
        using var document = StructureDocuments.Open(
            "/Threads [4 0 R]",
            "<< /Type /Thread /F 6 0 R /I << /Title (Man Bites Dog) >> >>",
            "<< /T 4 0 R /N 7 0 R /V 6 0 R /P 3 0 R /R [322 246 486 904] >>",
            "<< /T 4 0 R /N 5 0 R /V 7 0 R /P 3 0 R /R [158 247 318 905] >>",
            "<< /T 4 0 R /N 6 0 R /V 5 0 R /P 3 0 R /R [157 254 319 903] >>");
        var thread = PdfDocumentCatalog.GetThreads(document)[0];

        await Assert.That(thread.Title).IsEqualTo("Man Bites Dog");
        await Assert.That(thread.Beads.Length).IsEqualTo(ThreeBeads);
        await Assert.That(thread.Beads[0].Bounds!.Value.Left).IsEqualTo(FirstLeft);
        await Assert.That(thread.Beads[0].Bounds!.Value.Top).IsEqualTo(FirstTop);
        await Assert.That(thread.Beads[1].Bounds!.Value.Left).IsEqualTo(SecondLeft);
        await Assert.That(thread.Beads[Third].Bounds!.Value.Left).IsEqualTo(ThirdLeft);
        await Assert.That(thread.Beads[Third].Bounds!.Value.Bottom).IsEqualTo(ThirdBottom);
        await Assert.That(Array.TrueForAll(thread.Beads, static bead => bead.PageIndex == 0)).IsTrue();
    }

    /// <summary>A ring whose last bead points back into the middle, a bead with no /N and a /N to a missing object all stop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenBeadRingsStop()
    {
        using var document = StructureDocuments.Open(
            "/Threads [4 0 R 5 0 R 6 0 R]",
            "<< /F 7 0 R >>",
            "<< /F 10 0 R >>",
            "<< /F 12 0 R >>",
            "<< /N 8 0 R /P 3 0 R /R [0 0 1 1] >>",
            "<< /N 9 0 R /P 3 0 R /R [0 0 2 2] >>",
            "<< /N 8 0 R /P 3 0 R /R [0 0 3 3] >>",
            "<< /N 11 0 R /P 3 0 R /R [0 0 4 4] >>",
            "<< /P 3 0 R /R [0 0 5 5] >>",
            "<< /N 99 0 R /P 3 0 R /R [0 0 6 6] >>");
        var threads = PdfDocumentCatalog.GetThreads(document);

        await Assert.That(threads[0].Beads.Length).IsEqualTo(ThreeBeads);
        await Assert.That(threads[1].Beads.Length).IsEqualTo(TwoBeads);
        await Assert.That(threads[Third].Beads.Length).IsEqualTo(1);
    }

    /// <summary>Wrong-typed thread and bead entries give empty or null results instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongTypedThreadEntriesGiveEmptyResults()
    {
        using var document = StructureDocuments.Open(
            "/Threads [4 0 R (text) 7 5 0 R]",
            "<< /F 6 0 R /I (not a dictionary) >>",
            "<< /F 3 >>",
            "<< /N /Next /P 99 0 R /R (box) >>");
        using var named = StructureDocuments.Open("/Threads /None");
        var threads = PdfDocumentCatalog.GetThreads(document);

        await Assert.That(threads.Length).IsEqualTo(TwoThreads);
        await Assert.That(threads[0].Title).IsNull();
        await Assert.That(threads[0].Beads.Length).IsEqualTo(1);
        await Assert.That(threads[0].Beads[0].PageIndex).IsEqualTo(-1);
        await Assert.That(threads[0].Beads[0].Bounds).IsNull();
        await Assert.That(threads[1].Beads.Length).IsEqualTo(0);
        await Assert.That(PdfDocumentCatalog.GetThreads(named).Length).IsEqualTo(0);
    }

    /// <summary>Two page-piece producers keep their own dates, and malformed entries are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoPieceProducersKeepTheirDates()
    {
        using var document = StructureDocuments.OpenPage(
            "/PieceInfo 7",
            "/PieceInfo << /AppA << /LastModified (D:20240102030405Z) /Private << /Layer 1 >> >> /AppB 4 0 R /Broken 5 /List [1 2] /Undated << /LastModified 12 /Private (text) >> >>",
            "<< /LastModified (D:20250630120000+10'00') >>");
        var pieces = PdfDocumentCatalog.GetPieceInfo(document, PdfDocumentPages.GetPage(document, 0));

        await Assert.That(pieces.Select(static piece => piece.Name)).IsEquivalentTo(["AppA", "AppB", "Undated"]);
        await Assert.That(pieces[0].LastModified).IsEqualTo(DateTimeOffset.Parse("2024-01-02T03:04:05Z", CultureInfo.InvariantCulture));
        await Assert.That(pieces[0].Data).IsNotNull();
        await Assert.That(pieces[1].LastModified).IsEqualTo(DateTimeOffset.Parse("2025-06-30T12:00:00+10:00", CultureInfo.InvariantCulture));
        await Assert.That(pieces[1].LastModified!.Value.Offset).IsEqualTo(TimeSpan.Parse("10:00", CultureInfo.InvariantCulture));
        await Assert.That(pieces[1].Data).IsNull();
        await Assert.That(pieces[Third].LastModified).IsNull();
        await Assert.That(pieces[Third].Data).IsNull();
        await Assert.That(PdfDocumentCatalog.GetPieceInfo(document).Length).IsEqualTo(0);
    }

    /// <summary>A transition keeps the PDF defaults for missing entries, and wrong-typed entries fall back to them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransitionsKeepDefaultsAndStyles()
    {
        using var split = StructureDocuments.OpenPage(string.Empty, "/Dur 5 /Trans << /Type /Trans /D 3.5 /S /Split /Dm /V /M /O >>");
        using var empty = StructureDocuments.OpenPage(string.Empty, "/Trans << >> /Dur (soon)");
        using var wipe = StructureDocuments.OpenPage(string.Empty, "/Trans << /S /Wipe /Di 270 /D (long) >>");
        using var wrong = StructureDocuments.OpenPage(string.Empty, "/Trans /Split");
        var splitTransition = PdfDocumentViewerPreferences.GetTransition(split, PdfDocumentPages.GetPage(split, 0))!;
        var emptyTransition = PdfDocumentViewerPreferences.GetTransition(empty, PdfDocumentPages.GetPage(empty, 0))!;
        var wipeTransition = PdfDocumentViewerPreferences.GetTransition(wipe, PdfDocumentPages.GetPage(wipe, 0))!;

        await Assert.That(splitTransition.Style).IsEqualTo("Split");
        await Assert.That(splitTransition.Duration).IsEqualTo(SplitDuration);
        await Assert.That(splitTransition.Dimension).IsEqualTo("V");
        await Assert.That(splitTransition.Motion).IsEqualTo("O");
        await Assert.That(splitTransition.AdvanceAfter).IsEqualTo(SplitDisplay);
        await Assert.That(emptyTransition.Style).IsEqualTo("R");
        await Assert.That(emptyTransition.Duration).IsEqualTo(1);
        await Assert.That(emptyTransition.Dimension).IsEqualTo("H");
        await Assert.That(emptyTransition.Motion).IsEqualTo("I");
        await Assert.That(emptyTransition.Direction).IsEqualTo(0);
        await Assert.That(emptyTransition.Scale).IsEqualTo(1);
        await Assert.That(emptyTransition.Rectangular).IsFalse();
        await Assert.That(emptyTransition.AdvanceAfter).IsNull();
        await Assert.That(wipeTransition.Direction).IsEqualTo(TopToBottom);
        await Assert.That(wipeTransition.Duration).IsEqualTo(1);
        await Assert.That(PdfDocumentViewerPreferences.GetTransition(wrong, PdfDocumentPages.GetPage(wrong, 0))).IsNull();
    }

    /// <summary>Nested document parts keep their order and page ranges, and a one-page leaf ends where it starts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedDocumentPartsKeepOrderAndRanges()
    {
        // Objects: 1 catalog, 2 pages, 3-5 pages, 6 root, 7 part A, 8 part B, 9 leaf A1, 10 leaf A2.
        using var document = OpenParts(
            "<< /Type /DPart /Parent 11 0 R /DParts [[7 0 R] [8 0 R]] /DPM << /Kind /Root >> >>",
            "<< /Type /DPart /Parent 6 0 R /DParts [[9 0 R 10 0 R]] /DPM << /Recipient (Ann) >> >>",
            "<< /Type /DPart /Parent 6 0 R /Start 5 0 R /DPM << /Recipient (Bob) >> >>",
            "<< /Type /DPart /Parent 7 0 R /Start 3 0 R >>",
            "<< /Type /DPart /Parent 7 0 R /Start 4 0 R /End 4 0 R >>",
            "<< /Type /DPartRoot /DPartRootNode 6 0 R /RecordLevel 1 /NodeNameList [/Job /Recipient /Sheet] >>");
        var root = PdfDocumentWebCapture.GetDocumentParts(document)!;
        var top = root.Root!;

        await Assert.That(root.RecordLevel).IsEqualTo(1);
        await Assert.That(root.NodeNames).IsEquivalentTo(["Job", RecipientKey, "Sheet"]);
        await Assert.That(top.Metadata["Kind"]).IsEqualTo("Root");
        await Assert.That(top.Children.Length).IsEqualTo(TopParts);
        await Assert.That(top.Children[0].Metadata[RecipientKey]).IsEqualTo("Ann");
        await Assert.That(top.Children[0].StartPage).IsNull();
        await Assert.That(top.Children[0].Children[0].StartPage).IsEqualTo(0);
        await Assert.That(top.Children[0].Children[0].EndPage).IsEqualTo(0);
        await Assert.That(top.Children[0].Children[1].StartPage).IsEqualTo(1);
        await Assert.That(top.Children[0].Children[1].EndPage).IsEqualTo(1);
        await Assert.That(top.Children[1].StartPage).IsEqualTo(ThirdPage);
        await Assert.That(top.Children[1].EndPage).IsEqualTo(ThirdPage);
        await Assert.That(PdfDocumentWebCapture.GetDocumentPart(document, PdfDocumentPages.GetPage(document, ThirdPage))!.Metadata[RecipientKey]).IsEqualTo("Bob");
    }

    /// <summary>A document part tree that refers back to an ancestor or itself, or nests too deeply, stops within limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclicAndDeepDocumentPartTreesStop()
    {
        using var cyclic = OpenParts(
            "<< /DParts [[7 0 R 6 0 R]] >>",
            "<< /DParts [[6 0 R 7 0 R 8 0 R]] >>",
            "<< /DParts [[6 0 R]] /Start 3 0 R >>",
            "<< /DPartRootNode 6 0 R >>");
        var chain = new string[DeepChain + 1];
        for (var i = 0; i < DeepChain - 1; i++)
        {
            chain[i] = $"<< /DParts [[{FirstPartObject + i + 1} 0 R]] >>";
        }

        chain[DeepChain - 1] = "<< /Start 3 0 R >>";
        chain[DeepChain] = $"<< /DPartRootNode {FirstPartObject} 0 R >>";
        using var deep = OpenParts(chain);
        var top = PdfDocumentWebCapture.GetDocumentParts(cyclic)!.Root!;
        var node = PdfDocumentWebCapture.GetDocumentParts(deep)!.Root;
        var depth = 0;
        for (; node is { Children.Length: > 0 }; node = node.Children[0])
        {
            depth++;
        }

        await Assert.That(top.Children.Length).IsEqualTo(1);
        await Assert.That(top.Children[0].Children.Length).IsEqualTo(1);
        await Assert.That(top.Children[0].Children[0].Children.Length).IsEqualTo(0);
        await Assert.That(top.Children[0].Children[0].StartPage).IsEqualTo(0);
        await Assert.That(depth).IsEqualTo(MaxPartDepth);
    }

    /// <summary>Wrong-typed document part entries give empty or null results instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongTypedDocumentPartEntriesGiveEmptyResults()
    {
        using var document = OpenParts(
            "<< /DParts [7 0 R (text) [8 0 R /Name 5] 3] /DPM [1 2] >>",
            "<< /Start 6 0 R /End (last) >>",
            "<< /DParts /None /Start 4 >>",
            "<< /DPartRootNode 6 0 R /RecordLevel (one) /NodeNameList (names) >>");
        using var missing = OpenParts("<< /DPartRootNode 5 >>");
        var root = PdfDocumentWebCapture.GetDocumentParts(document)!;

        await Assert.That(root.RecordLevel).IsEqualTo(0);
        await Assert.That(root.NodeNames.Length).IsEqualTo(0);
        await Assert.That(root.Root!.Metadata.Count).IsEqualTo(0);
        await Assert.That(root.Root.Children.Length).IsEqualTo(TwoChildren);
        await Assert.That(root.Root.Children[0].StartPage).IsNull();
        await Assert.That(root.Root.Children[0].EndPage).IsNull();
        await Assert.That(root.Root.Children[1].Children.Length).IsEqualTo(0);
        await Assert.That(root.Root.Children[1].StartPage).IsNull();
        await Assert.That(PdfDocumentWebCapture.GetDocumentParts(missing)!.Root).IsNull();
        await Assert.That(PdfDocumentWebCapture.GetDocumentPart(missing, PdfDocumentPages.GetPage(missing, ThirdPage))).IsNull();
    }

    /// <summary>Web capture name tree entries may hold one content set or an array of them; shared sets count once and malformed entries are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WebCaptureContentSetArraysAndSourcesAreRead()
    {
        using var document = StructureDocuments.Open(
            "/SpiderInfo << /V 1.1 >> /Names << /IDS << /Names [(a) [4 0 R 5 0 R] (b) 6 0 R (c) 7] >> /URLS << /Names [(http://x/) 4 0 R] >> >>",
            "<< /Type /SpiderContentSet /S /SPS /ID <0102> /O [3 0 R] /SI [<< /AU (http://x/) /TS (D:20230101000000Z) /S 0 >> << /AU (http://x/form) /S 2 >>] >>",
            "<< /Type /SpiderContentSet /S /SIS /ID (img) /O [] /SI << /AU (http://x/a.gif) >> >>",
            "<< /S /SPS /ID (third) /SI 5 >>");
        using var wrong = StructureDocuments.Open("/SpiderInfo (v) /Names << /IDS << /Names [(a) 5 (b) (text)] >> >>");
        var web = PdfDocumentWebCapture.GetWebCapture(document)!;
        var first = web.ContentSets[0];

        await Assert.That(web.Version).IsEqualTo(CaptureVersion);
        await Assert.That(web.IdEntryCount).IsEqualTo(IdEntries);
        await Assert.That(web.UrlEntryCount).IsEqualTo(1);
        await Assert.That(web.ContentSets.Select(static set => set.Subtype)).IsEquivalentTo(["SPS", "SIS", "SPS"]);
        await Assert.That(web.ContentSets.Length).IsEqualTo(ContentSets);
        await Assert.That(first.Id).IsEquivalentTo(FirstSetId);
        await Assert.That(first.ObjectCount).IsEqualTo(1);
        await Assert.That(first.Sources.Length).IsEqualTo(Sources);
        await Assert.That(first.Sources[0].Timestamp!.Value.Year).IsEqualTo(CaptureYear);
        await Assert.That(first.Sources[1].Url).IsEqualTo("http://x/form");
        await Assert.That(first.Sources[1].Type).IsEqualTo(PostSource);
        await Assert.That(Encoding.ASCII.GetString(web.ContentSets[1].Id)).IsEqualTo("img");
        await Assert.That(web.ContentSets[Third].Sources.Length).IsEqualTo(0);
        await Assert.That(web.ContentSets[Third].ObjectCount).IsEqualTo(0);
        await Assert.That(PdfDocumentWebCapture.GetWebCapture(wrong)!.ContentSets.Length).IsEqualTo(0);
        await Assert.That(PdfDocumentWebCapture.GetWebCapture(wrong)!.Version).IsEqualTo(0);
    }

    /// <summary>Opens a three-page document whose catalog's /DPartRoot is the last of <paramref name="extra"/>. The third page's /DPart is object 8.</summary>
    /// <param name="extra">The objects numbered from 6; the last is the DPartRoot dictionary.</param>
    /// <returns>The document.</returns>
    private static PdfDocument OpenParts(params string[] extra)
    {
        string[] header =
        [
            $"<< /Type /Catalog /Pages 2 0 R /DPartRoot {FirstPartObject + extra.Length - 1} 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /DPart 8 0 R >>",
        ];
        return PdfDocumentReader.Open(MiniPdf.Build([.. header, .. extra]), null);
    }
}
