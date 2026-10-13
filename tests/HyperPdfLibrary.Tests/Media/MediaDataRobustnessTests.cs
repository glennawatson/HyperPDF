// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Tests.Document;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>
/// Tests that damaged media, 3D, rich media, JavaScript and XFA entries read as bounded data without throwing, and that
/// reading only describes them (ISO 32000-2 §12.6.4, §12.7.8, §13.2, §13.6, §13.7).
/// </summary>
public sealed class MediaDataRobustnessTests
{
    /// <summary>The longest clip section chain a read follows.</summary>
    private const int MaxSectionDepth = 8;

    /// <summary>The number of chained clip sections in the long sample.</summary>
    private const int ChainLength = 100;

    /// <summary>The first object number of the chain sample's clips.</summary>
    private const int ChainStart = 5;

    /// <summary>The XFA packets that survive in the mixed sample.</summary>
    private const int SurvivingPackets = 1;

    /// <summary>The stream entries that name a filter the stream data does not match.</summary>
    private const string FlateEntries = "/Filter /FlateDecode";

    /// <summary>Stream data that is not a Flate stream.</summary>
    private const string NotFlateData = "not flate data";

    /// <summary>The page entries that list one annotation, object 4.</summary>
    private const string OneAnnotation = "/Annots [4 0 R]";

    /// <summary>A multimedia annotation of any kind is read, with every field defaulted, from a dictionary that holds nothing else.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <param name="kind">The kind expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Screen", PdfMultimediaKind.Screen)]
    [Arguments("Movie", PdfMultimediaKind.Movie)]
    [Arguments("Sound", PdfMultimediaKind.Sound)]
    [Arguments("RichMedia", PdfMultimediaKind.RichMedia)]
    [Arguments("3D", PdfMultimediaKind.ThreeD)]
    public async Task BareAnnotationsReadWithDefaults(string subtype, PdfMultimediaKind kind)
    {
        using var document = StructureDocuments.OpenPage(string.Empty, OneAnnotation, $"<< /Type /Annot /Subtype /{subtype} >>");

        var annotation = PdfDocumentMedia.GetMultimediaAnnotations(document).Single();

        await Assert.That(annotation.Kind).IsEqualTo(kind);
        await Assert.That(annotation.Bounds).IsNull();
        await Assert.That(annotation.Sound).IsNull();
        await Assert.That(annotation.ThreeD?.Stream).IsNull();
        await Assert.That(annotation.RichMedia?.Assets ?? []).IsEmpty();
    }

    /// <summary>Wrongly typed members of each annotation kind read as absent or defaulted.</summary>
    /// <param name="entries">The annotation entries.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Subtype /Screen /T 5 /A 7 /AA 9")]
    [Arguments("/Subtype /Screen /A << /S /Rendition /R 5 /OP (x) >>")]
    [Arguments("/Subtype /Movie /Movie 42 /A true")]
    [Arguments("/Subtype /Movie /Movie << /F 5 /Aspect [(a) /b 3] /Rotate (x) /Poster [1] >> /A << /Start [1] /Rate (x) /FWScale (y) /FWPosition [/z] >>")]
    [Arguments("/Subtype /Sound /Sound 42")]
    [Arguments("/Subtype /Sound /Sound << /R 5 >>")]
    [Arguments("/Subtype /RichMedia /RichMediaContent 5 /RichMediaSettings (x)")]
    [Arguments("/Subtype /RichMedia /RichMediaContent << /Assets 5 /Configurations [1 (x) << /Instances 7 >> << /Instances [3 << /Asset 5 >>] >>] /Views 9 >>")]
    [Arguments("/Subtype /RichMedia /RichMediaContent << /Assets << /Names [(a) 5 (b)] >> >> /RichMediaSettings << /Activation 5 >>")]
    [Arguments("/Subtype /3D /3DD 42 /3DA (x) /3DV [1]")]
    [Arguments("/Subtype /3D /3DD << /3DD 7 >> /3DA << /A 5 /TB 7 >> /3DV << /C2W (x) /P 5 /BG 7 >>")]
    [Arguments("/Subtype /3D /3DD << /Subtype 5 /VA [1 (x) << /C2W [(a)] /SA 5 >>] /DV /Last >> /3DV /First")]
    public async Task WronglyTypedMembersAreReadAsData(string entries)
    {
        using var document = StructureDocuments.OpenPage(string.Empty, OneAnnotation, $"<< /Type /Annot {entries} >>");

        var annotations = PdfDocumentMedia.GetMultimediaAnnotations(document);

        await Assert.That(annotations.Length).IsEqualTo(1);
    }

    /// <summary>A 3D annotation whose data is a damaged stream is described, and its bytes are read only when asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreeDStreamWithABadFilterIsDescribedWithoutDecoding()
    {
        using var document = StructureDocuments.OpenPage(
            string.Empty,
            OneAnnotation,
            "<< /Type /Annot /Subtype /3D /3DD 5 0 R >>",
            MiniPdf.Stream($"/Type /3D /Subtype /U3D {FlateEntries}", NotFlateData));

        var annotation = PdfDocumentMedia.GetMultimediaAnnotations(document).Single();

        await Assert.That(annotation.ThreeD!.Stream!.Subtype).IsEqualTo("U3D");
        await Assert.That(annotation.ThreeD.Stream.Data.RawLength).IsGreaterThan(0);
    }

    /// <summary>A rendition whose clip sections form a very long chain, or a loop, is followed only to the depth limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipSectionChainsStopAtTheDepthLimit()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] {OneAnnotation} >>",
            $"<< /Type /Annot /Subtype /Screen /A << /S /Rendition /OP 0 /R << /S /MR /C {ChainStart} 0 R >> >> >>",
        };
        for (var i = 0; i < ChainLength; i++)
        {
            objects.Add($"<< /S /MCS /N (section) /D {ChainStart + i + 1} 0 R >>");
        }

        objects.Add($"<< /S /MCS /D {ChainStart} 0 R >>");
        using var document = PdfDocumentReader.Open(MiniPdf.Build([.. objects]), null);

        var action = PdfDocumentMedia.GetMultimediaAnnotations(document).Single().Screen!.Action!;
        var rendition = ((RenditionAction)action.Action.Value!).Rendition;

        await Assert.That(CountSections(rendition?.Clip)).IsLessThanOrEqualTo(MaxSectionDepth + 1);
    }

    /// <summary>XFA entries of the wrong shape are skipped, a damaged packet stream reads empty, and the rest are kept.</summary>
    /// <param name="xfa">The <c>/XFA</c> value.</param>
    /// <param name="packets">The packets expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[(template) 4 0 R]", 1)]
    [Arguments("[(template)]", 0)]
    [Arguments("[1 4 0 R (x) 7 (y)]", 0)]
    [Arguments("[(a) 4 0 R (b) 4 0 R (c)]", 2)]
    [Arguments("[]", 0)]
    [Arguments("[(template) 5 0 R]", 1)]
    public async Task XfaArraysOfTheWrongShapeAreSkipped(string xfa, int packets)
    {
        using var document = StructureDocuments.Open(
            $"/AcroForm << /Fields [] /XFA {xfa} >>",
            MiniPdf.Stream(string.Empty, "<xdp/>"),
            MiniPdf.Stream(FlateEntries, NotFlateData));

        var read = PdfDocumentXfa.GetXfa(document)!;

        await Assert.That(read.Packets.Count).IsEqualTo(packets);
        await Assert.That(read.IsSingleStream).IsFalse();
    }

    /// <summary>An XFA value that is not an array or a stream, or a missing one, gives no packets.</summary>
    /// <param name="xfa">The <c>/XFA</c> entry text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/XFA 42")]
    [Arguments("/XFA (text)")]
    [Arguments("/XFA << /A 1 >>")]
    [Arguments("/XFA null")]
    [Arguments("")]
    public async Task NonArrayXfaGivesNone(string xfa)
    {
        using var document = StructureDocuments.Open($"/AcroForm << /Fields [] {xfa} >>");

        await Assert.That(PdfDocumentXfa.GetXfa(document)).IsNull();
    }

    /// <summary>A damaged single XFA stream reads as an empty packet and a wide-character name is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedSingleXfaStreamReadsEmpty()
    {
        using var document = StructureDocuments.Open("/AcroForm << /Fields [] /XFA 4 0 R >>", MiniPdf.Stream(FlateEntries, NotFlateData));

        var read = PdfDocumentXfa.GetXfa(document)!;

        await Assert.That(read.IsSingleStream).IsTrue();
        await Assert.That(read.Packets.Single().Data).IsEmpty();
        await Assert.That(read.Packets.Count).IsEqualTo(SurvivingPackets);
    }

    /// <summary>Document script entries of the wrong shape are skipped, and each valid one keeps its name and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentScriptsOfTheWrongShapeAreSkipped()
    {
        using var document = StructureDocuments.Open(
            "/Names << /JavaScript 4 0 R >>",
            "<< /Names [(ok) 5 0 R (nojs) 6 0 R (number) 7 0 R (array) 8 0 R (streamed) 9 0 R (broken) 10 0 R (null) null (str) (just a string)] >>",
            "<< /S /JavaScript /JS (var ok = 1;) >>",
            "<< /S /JavaScript >>",
            "<< /S /JavaScript /JS 42 >>",
            "<< /S /JavaScript /JS [(a) (b)] >>",
            "<< /S /JavaScript /JS 11 0 R >>",
            "<< /S /JavaScript /JS 12 0 R >>",
            MiniPdf.Stream(string.Empty, "var streamed = 2;"),
            MiniPdf.Stream(FlateEntries, NotFlateData));
        using var plain = StructureDocuments.Open(string.Empty);

        var scripts = PdfDocumentActions.GetDocumentScripts(document);

        await Assert.That(scripts.Select(static script => script.Name)).Contains("ok").And.Contains("streamed");
        await Assert.That(scripts.First(static script => script.Name == "ok").Script).IsEqualTo("var ok = 1;");
        await Assert.That(scripts.First(static script => script.Name == "streamed").Script).IsEqualTo("var streamed = 2;");
        await Assert.That(scripts.Select(static script => script.Name)).DoesNotContain("nojs").And.DoesNotContain("number");
        await Assert.That(PdfDocumentActions.GetDocumentScripts(plain)).IsEmpty();
    }

    /// <summary>A name tree of document scripts that loops through its kids stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopingScriptTreeStops()
    {
        using var document = StructureDocuments.Open(
            "/Names << /JavaScript 4 0 R >>",
            "<< /Kids [5 0 R] >>",
            "<< /Kids [4 0 R 5 0 R] /Names [(x) 6 0 R] >>",
            "<< /S /JavaScript /JS (var x;) >>");

        var scripts = PdfDocumentActions.GetDocumentScripts(document);

        await Assert.That(scripts.Length).IsLessThanOrEqualTo(1);
    }

    /// <summary>Counts clip sections down a chain.</summary>
    /// <param name="clip">The clip, or null.</param>
    /// <returns>The number of clips in the chain.</returns>
    private static int CountSections(PdfMediaClip? clip)
    {
        var count = 0;
        for (var current = clip; current is not null; current = current.Section)
        {
            count++;
        }

        return count;
    }
}
