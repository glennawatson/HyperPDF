// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for XFA, threads, viewer preferences, transitions, output intents, permissions, piece info, web capture, document parts and measures.</summary>
public sealed class CatalogStructureTests
{
    /// <summary>The left edge of the second sample bead.</summary>
    private const float SecondBeadLeft = 20;

    /// <summary>The precision the sample distance format sets.</summary>
    private const int DistancePrecision = 10;

    /// <summary>The precision a number format has when it sets none.</summary>
    private const int DefaultPrecision = 100;

    /// <summary>The number of packets in the sample XFA array.</summary>
    private const int PacketCount = 2;

    /// <summary>The number of copies the sample preferences ask for.</summary>
    private const int Copies = 3;

    /// <summary>The duration of the sample transition.</summary>
    private const double TransitionDuration = 2.5;

    /// <summary>The automatic advance time of the sample page.</summary>
    private const double AdvanceSeconds = 3;

    /// <summary>The number of components in the sample profile.</summary>
    private const int Components = 3;

    /// <summary>The count of JavaScript actions in the sample legal attestation.</summary>
    private const long LegalCount = 2;

    /// <summary>The EPSG code in the sample geospatial measure.</summary>
    private const int Epsg = 4326;

    /// <summary>The x origin in the sample measure.</summary>
    private const double OriginX = 1;

    /// <summary>The y origin in the sample measure.</summary>
    private const double OriginY = 2;

    /// <summary>The number of values in the sample ground points.</summary>
    private const int PointValues = 4;

    /// <summary>An XFA array gives its packets, and a single stream gives one packet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaPacketsAreKeptRaw()
    {
        using var array = StructureDocuments.Open(
            "/AcroForm << /Fields [] /XFA [(template) 4 0 R (datasets) 5 0 R] >>",
            MiniPdf.Stream(string.Empty, "<template/>"),
            MiniPdf.Stream(string.Empty, "<data/>"));
        using var single = StructureDocuments.Open("/AcroForm << /Fields [] /XFA 4 0 R >>", MiniPdf.Stream(string.Empty, "<xdp/>"));
        using var none = StructureDocuments.Open(string.Empty);

        var packets = HyperPdfLibrary.Document.PdfDocumentXfa.GetXfa(array)!;

        await Assert.That(HyperPdfLibrary.Document.PdfDocumentXfa.HasXfa(array)).IsTrue();
        await Assert.That(packets.Packets.Count).IsEqualTo(PacketCount);
        await Assert.That(System.Text.Encoding.ASCII.GetString(packets.Find("datasets")!.Data.AsSpan())).IsEqualTo("<data/>");
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentXfa.GetXfa(single)!.IsSingleStream).IsTrue();
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentXfa.GetXfa(single)!.Packets[0].Name).IsEqualTo("xdp");
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentXfa.GetXfa(none)).IsNull();
    }

    /// <summary>A thread's bead ring is read once, with pages and rectangles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreadBeadsAreRead()
    {
        using var document = StructureDocuments.Open(
            "/Threads [4 0 R]",
            "<< /Type /Thread /F 5 0 R /I << /Title (Story) >> >>",
            "<< /T 4 0 R /N 6 0 R /V 6 0 R /P 3 0 R /R [0 0 10 10] >>",
            "<< /T 4 0 R /N 5 0 R /V 5 0 R /P 3 0 R /R [20 20 30 30] >>");
        var thread = HyperPdfLibrary.Document.PdfDocumentCatalog.GetThreads(document)[0];

        await Assert.That(thread.Title).IsEqualTo("Story");
        await Assert.That(thread.Beads.Length).IsEqualTo(PacketCount);
        await Assert.That(thread.Beads[0].PageIndex).IsEqualTo(0);
        await Assert.That(thread.Beads[1].Bounds!.Value.Left).IsEqualTo(SecondBeadLeft);
    }

    /// <summary>Viewer preferences, page mode and layout, and transitions keep the PDF defaults when absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewerPreferencesAndTransitionsAreRead()
    {
        using var document = StructureDocuments.OpenPage(
            "/PageMode /UseOutlines /PageLayout /TwoColumnLeft /ViewerPreferences << /HideToolbar true /Direction /R2L /NumCopies 3 /PrintPageRange [1 2] /Enforce [/PrintScaling] >>",
            "/Trans << /S /Fly /D 2.5 /Di /None /B true >> /Dur 3");
        using var plain = StructureDocuments.Open(string.Empty);
        var prefs = HyperPdfLibrary.Document.PdfDocumentViewerPreferences.GetViewerPreferences(document);
        var transition = HyperPdfLibrary.Document.PdfDocumentViewerPreferences.GetTransition(document, HyperPdfLibrary.Document.PdfDocumentPages.GetPage(document, 0))!;

        await Assert.That(prefs.PageMode).IsEqualTo("UseOutlines");
        await Assert.That(prefs.PageLayout).IsEqualTo("TwoColumnLeft");
        await Assert.That(prefs.HideToolbar).IsTrue();
        await Assert.That(prefs.Direction).IsEqualTo("R2L");
        await Assert.That(prefs.NumCopies).IsEqualTo(Copies);
        await Assert.That(prefs.PrintPageRange).IsEquivalentTo([1, PacketCount]);
        await Assert.That(prefs.Enforce).IsEquivalentTo(["PrintScaling"]);
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentViewerPreferences.GetViewerPreferences(plain).PageMode).IsEqualTo("UseNone");
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentViewerPreferences.GetViewerPreferences(plain).NumCopies).IsEqualTo(1);
        await Assert.That(transition.Style).IsEqualTo("Fly");
        await Assert.That(transition.Duration).IsEqualTo(TransitionDuration);
        await Assert.That(transition.Direction).IsEqualTo(-1);
        await Assert.That(transition.Rectangular).IsTrue();
        await Assert.That(transition.AdvanceAfter).IsEqualTo(AdvanceSeconds);
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentViewerPreferences.GetTransition(plain, HyperPdfLibrary.Document.PdfDocumentPages.GetPage(plain, 0))).IsNull();
    }

    /// <summary>Output intents, permissions and piece info are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IntentsPermissionsAndPiecesAreRead()
    {
        using var document = StructureDocuments.OpenPage(
            "/OutputIntents [4 0 R] /Perms << /DocMDP 6 0 R >> /Legal << /JavaScriptActions 2 /Attestation (hi) >>",
            "/PieceInfo << /MyApp << /LastModified (D:20240102030405Z) /Private << /X 1 >> >> >>",
            "<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier (sRGB) /DestOutputProfile 5 0 R >>",
            MiniPdf.Stream("/N 3", "icc"),
            "<< /Type /Sig /Reference [<< /TransformMethod /DocMDP /TransformParams << /P 1 >> >>] >>");
        var intent = HyperPdfLibrary.Document.PdfDocumentCatalog.GetOutputIntents(document)[0];
        var permissions = HyperPdfLibrary.Document.PdfDocumentCatalog.GetPermissions(document)!;
        var piece = HyperPdfLibrary.Document.PdfDocumentCatalog.GetPieceInfo(document, HyperPdfLibrary.Document.PdfDocumentPages.GetPage(document, 0))[0];

        await Assert.That(intent.Subtype).IsEqualTo("GTS_PDFA1");
        await Assert.That(intent.OutputConditionIdentifier).IsEqualTo("sRGB");
        await Assert.That(intent.ComponentCount).IsEqualTo(Components);
        await Assert.That(intent.Profile).IsNotNull();
        await Assert.That(permissions.HasDocMdp).IsTrue();
        await Assert.That(permissions.DocMdpLevel).IsEqualTo(1);
        await Assert.That(permissions.HasUsageRights).IsFalse();
        await Assert.That(permissions.Legal["JavaScriptActions"]).IsEqualTo(LegalCount);
        await Assert.That(permissions.Attestation).IsEqualTo("hi");
        await Assert.That(piece.Name).IsEqualTo("MyApp");
        await Assert.That(piece.LastModified).IsNotNull();
        await Assert.That(piece.Data).IsNotNull();
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentCatalog.GetPieceInfo(document).Length).IsEqualTo(0);
    }

    /// <summary>Web capture content sets and document parts are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WebCaptureAndDocumentPartsAreRead()
    {
        using var capture = StructureDocuments.Open(
            "/SpiderInfo << /V 1.0 >> /Names << /IDS << /Names [(id1) 4 0 R] >> >>",
            "<< /Type /SpiderContentSet /S /SPS /ID (id1) /CT (text/html) /SI << /AU (http://x) /S 0 >> /O [3 0 R] >>");
        using var parts = StructureDocuments.Open(
            "/DPartRoot << /DPartRootNode 4 0 R /RecordLevel 1 /NodeNameList [/Root /Doc] >>",
            "<< /DParts [[5 0 R]] /DPM << /Title (T) >> >>",
            "<< /Parent 4 0 R /Start 3 0 R /End 3 0 R /DPM << /N 2 >> >>");
        var web = HyperPdfLibrary.Document.PdfDocumentWebCapture.GetWebCapture(capture)!;
        var root = HyperPdfLibrary.Document.PdfDocumentWebCapture.GetDocumentParts(parts)!;

        await Assert.That(web.Version).IsEqualTo(1.0);
        await Assert.That(web.ContentSets[0].Subtype).IsEqualTo("SPS");
        await Assert.That(web.ContentSets[0].Sources[0].Url).IsEqualTo("http://x");
        await Assert.That(web.ContentSets[0].ObjectCount).IsEqualTo(1);
        await Assert.That(root.NodeNames).IsEquivalentTo(["Root", "Doc"]);
        await Assert.That(root.Root!.Metadata["Title"]).IsEqualTo("T");
        await Assert.That(root.Root.Children[0].StartPage).IsEqualTo(0);
        await Assert.That(root.Root.Children[0].Metadata["N"]).IsEqualTo("2");
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentWebCapture.GetWebCapture(StructureDocuments.Open(string.Empty))).IsNull();
    }

    /// <summary>Viewports read rectilinear and geospatial measures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewportMeasuresAreRead()
    {
        using var document = StructureDocuments.OpenPage(
            string.Empty,
            "/VP [<< /Type /Viewport /BBox [0 0 50 50] /Name (V) /Measure 4 0 R >> << /Type /Viewport /Measure 5 0 R >>]",
            "<< /Type /Measure /Subtype /RL /R (1 in = 1 mi) /X [<< /U (mi) /C 1 >>] /D [<< /U (mi) /C 1 /D 10 >>] /O [1 2] >>",
            "<< /Type /Measure /Subtype /GEO /GCS << /Type /GEOGCS /EPSG 4326 >> /GPTS [1 2 3 4] /LPTS [0 0 1 1] /PDU [/M /KM] >>");
        var viewports = HyperPdfLibrary.Document.PdfDocumentMeasure.GetViewports(document, HyperPdfLibrary.Document.PdfDocumentPages.GetPage(document, 0));
        var rectilinear = viewports[0].Measure!;
        var geo = viewports[1].Measure!.Geo!;

        await Assert.That(viewports[0].Name).IsEqualTo("V");
        await Assert.That(rectilinear.Ratio).IsEqualTo("1 in = 1 mi");
        await Assert.That(rectilinear.X[0].Unit).IsEqualTo("mi");
        await Assert.That(rectilinear.Distance[0].Precision).IsEqualTo(DistancePrecision);
        await Assert.That(rectilinear.X[0].Precision).IsEqualTo(DefaultPrecision);
        await Assert.That(rectilinear.Origin).IsEquivalentTo([OriginX, OriginY]);
        await Assert.That(geo.CoordinateSystemType).IsEqualTo("GEOGCS");
        await Assert.That(geo.Epsg).IsEqualTo(Epsg);
        await Assert.That(geo.GeoPoints.Length).IsEqualTo(PointValues);
        await Assert.That(geo.DisplayUnits).IsEquivalentTo(["M", "KM"]);
    }
}
