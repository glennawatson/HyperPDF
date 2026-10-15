// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>
/// Tests that the content scan records each image where it lands, whether or not its data can be decoded, and counts it as
/// untagged only outside marked content with an id and outside artifacts.
/// </summary>
public sealed class AccessibilityImageScanTests
{
    /// <summary>The side of every test image, in points.</summary>
    private const float Side = 40F;

    /// <summary>How far the recorded bounds may differ from the drawn square.</summary>
    private const float Tolerance = 0.5F;

    /// <summary>The marked content id the figure wrapper uses; the default page uses ids 0 to 2 for its lines and figure.</summary>
    private const int FigureId = 3;

    /// <summary>Draws the image resource at 40 by 40 points.</summary>
    private const string DrawXObject = "q 40 0 0 40 300 300 cm /Im0 Do Q\n";

    /// <summary>An inline image whose data decodes.</summary>
    private const string DecodableInline = "q 40 0 0 40 300 300 cm BI /W 1 /H 1 /CS /G /BPC 8 /F /AHx ID 80> EI Q\n";

    /// <summary>An inline image whose Flate data is not a valid stream.</summary>
    private const string UndecodableInline = "q 40 0 0 40 300 300 cm BI /W 1 /H 1 /CS /G /BPC 8 /F /Fl ID notflate EI Q\n";

    /// <summary>The kinds of image the tests draw.</summary>
    public enum ImageKind
    {
        /// <summary>An inline image whose data decodes.</summary>
        InlineDecodable = 0,

        /// <summary>An inline image whose data cannot be decoded.</summary>
        InlineUndecodable = 1,

        /// <summary>An image XObject whose data decodes.</summary>
        XObjectDecodable = 2,

        /// <summary>An image XObject whose data cannot be decoded.</summary>
        XObjectUndecodable = 3,
    }

    /// <summary>Where the tests draw an image.</summary>
    public enum Place
    {
        /// <summary>Outside marked content.</summary>
        Loose = 0,

        /// <summary>Inside a tagged sequence that has an id.</summary>
        Tagged = 1,

        /// <summary>Inside an artifact.</summary>
        Artifact = 2,
    }

    /// <summary>An image outside marked content is recorded at its place and counted once as untagged, however it is stored.</summary>
    /// <param name="kind">The kind of image.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ImageKind.InlineDecodable)]
    [Arguments(ImageKind.InlineUndecodable)]
    [Arguments(ImageKind.XObjectDecodable)]
    [Arguments(ImageKind.XObjectUndecodable)]
    public async Task LooseImageIsRecordedAndCountedAsUntagged(ImageKind kind, CancellationToken cancellationToken)
    {
        var spec = SpecFor(kind, Place.Loose);
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(spec));

        var page = PdfDocumentTagged.GetMarkedContent(document, 0);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(page.Images.Length).IsEqualTo(1);
        await Assert.That(page.Images[0].Mcid).IsEqualTo(-1);
        await Assert.That(page.Images[0].IsArtifact).IsFalse();
        await Assert.That(Math.Abs(page.Images[0].Bounds.Width - Side)).IsLessThan(Tolerance);
        await Assert.That(Math.Abs(page.Images[0].Bounds.Height - Side)).IsLessThan(Tolerance);
        await Assert.That(page.UntaggedGraphicCount).IsEqualTo(1);
        await Assert.That(report.Pages[0].UntaggedGraphics).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(1);
    }

    /// <summary>An image inside a tagged sequence is recorded with its id and is not counted as untagged.</summary>
    /// <param name="kind">The kind of image.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ImageKind.InlineDecodable)]
    [Arguments(ImageKind.InlineUndecodable)]
    [Arguments(ImageKind.XObjectDecodable)]
    [Arguments(ImageKind.XObjectUndecodable)]
    public async Task TaggedImageIsRecordedWithItsIdAndNotCounted(ImageKind kind, CancellationToken cancellationToken)
    {
        var spec = SpecFor(kind, Place.Tagged);
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(spec));

        var page = PdfDocumentTagged.GetMarkedContent(document, 0);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(page.Images.Length).IsEqualTo(1);
        await Assert.That(page.Images[0].Mcid).IsEqualTo(FigureId);
        await Assert.That(page.Images[0].IsArtifact).IsFalse();
        await Assert.That(page.UntaggedGraphicCount).IsEqualTo(0);
        await Assert.That(report.Pages[0].UntaggedGraphics).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(0);
    }

    /// <summary>An image inside an artifact is recorded as an artifact and is not counted as untagged.</summary>
    /// <param name="kind">The kind of image.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ImageKind.InlineDecodable)]
    [Arguments(ImageKind.InlineUndecodable)]
    [Arguments(ImageKind.XObjectDecodable)]
    [Arguments(ImageKind.XObjectUndecodable)]
    public async Task ArtifactImageIsRecordedAsAnArtifactAndNotCounted(ImageKind kind, CancellationToken cancellationToken)
    {
        var spec = SpecFor(kind, Place.Artifact);
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(spec));

        var page = PdfDocumentTagged.GetMarkedContent(document, 0);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(page.Images.Length).IsEqualTo(1);
        await Assert.That(page.Images[0].IsArtifact).IsTrue();
        await Assert.That(page.UntaggedGraphicCount).IsEqualTo(0);
        await Assert.That(report.Pages[0].UntaggedGraphics).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(0);
    }

    /// <summary>The untagged count and the recorded place are the same whether the image data decodes or not.</summary>
    /// <param name="place">Where the image is drawn.</param>
    /// <param name="inline">Whether the image is inline.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Place.Loose, true)]
    [Arguments(Place.Loose, false)]
    [Arguments(Place.Tagged, true)]
    [Arguments(Place.Tagged, false)]
    [Arguments(Place.Artifact, true)]
    [Arguments(Place.Artifact, false)]
    public async Task DecodableAndUndecodableImagesGiveTheSameCounts(Place place, bool inline, CancellationToken cancellationToken)
    {
        var good = SpecFor(inline ? ImageKind.InlineDecodable : ImageKind.XObjectDecodable, place);
        var bad = SpecFor(inline ? ImageKind.InlineUndecodable : ImageKind.XObjectUndecodable, place);
        using var goodDocument = TaggedTestDocument.Open(AccessibilityPdfs.Build(good));
        using var badDocument = TaggedTestDocument.Open(AccessibilityPdfs.Build(bad));

        var goodReport = await PdfDocumentAccessibility.GetAccessibilityReportAsync(goodDocument, cancellationToken);
        var badReport = await PdfDocumentAccessibility.GetAccessibilityReportAsync(badDocument, cancellationToken);
        var goodPage = PdfDocumentTagged.GetMarkedContent(goodDocument, 0);
        var badPage = PdfDocumentTagged.GetMarkedContent(badDocument, 0);

        await Assert.That(badReport.Pages[0].UntaggedGraphics).IsEqualTo(goodReport.Pages[0].UntaggedGraphics);
        await Assert.That(badReport.GetCount(PdfAccessibilityCode.UntaggedContent)).IsEqualTo(goodReport.GetCount(PdfAccessibilityCode.UntaggedContent));
        await Assert.That(badPage.UntaggedGraphicCount).IsEqualTo(goodPage.UntaggedGraphicCount);
        await Assert.That(badPage.Images.Length).IsEqualTo(goodPage.Images.Length);
        await Assert.That(badPage.Images[0].Bounds).IsEqualTo(goodPage.Images[0].Bounds);
    }

    /// <summary>Builds the spec for one kind of image in one place.</summary>
    /// <param name="kind">The kind of image.</param>
    /// <param name="place">Where it is drawn.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec SpecFor(ImageKind kind, Place place)
    {
        var draw = kind switch
        {
            ImageKind.InlineDecodable => DecodableInline,
            ImageKind.InlineUndecodable => UndecodableInline,
            _ => DrawXObject,
        };
        var wrapped = place switch
        {
            Place.Tagged => string.Create(CultureInfo.InvariantCulture, $"/Figure <</MCID {FigureId}>> BDC\n{draw}EMC\n"),
            Place.Artifact => $"/Artifact BMC\n{draw}EMC\n",
            _ => draw,
        };
        var content = new AccessibilitySpec().Content + wrapped;
        return kind is ImageKind.XObjectDecodable or ImageKind.XObjectUndecodable
            ? new() { Content = content, Layout = (pdf, document) => XObjectLayout(pdf, document, kind == ImageKind.XObjectDecodable) }
            : new() { Content = content };
    }

    /// <summary>Adds the image XObject the content draws and the default structure.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element's number.</param>
    /// <param name="decodable">Whether the image data decodes.</param>
    /// <returns>The document element's kids.</returns>
    private static string XObjectLayout(TaggedPdfBuilder pdf, int document, bool decodable)
    {
        var image = decodable
            ? pdf.Add(MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "80>"))
            : pdf.Add(MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", "notflate"));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Im0 {image} 0 R >>");
        return AccessibilityPdfs.DefaultLayout(pdf, document);
    }
}
