// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests finding content the viewer cannot show or run.</summary>
public sealed class ContentCheckTests
{
    /// <summary>A page index past the end of every sample.</summary>
    private const int MissingPage = 5;

    /// <summary>The width and height in pixels of the pages the unknown filter pages draw, which are 100 points square.</summary>
    private const int PageSide = 100;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The colour channels in one pixel; the fourth byte is opacity.</summary>
    private const int RgbBytes = 3;

    /// <summary>The level below which a colour channel counts as black.</summary>
    private const int DarkLimit = 0x40;

    /// <summary>The part of a page that uses an unknown filter.</summary>
    public enum UnknownFilterPart
    {
        /// <summary>Nothing does.</summary>
        None = 0,

        /// <summary>The page's content stream.</summary>
        Content = 1,

        /// <summary>A form the page draws.</summary>
        Form = 2,

        /// <summary>A font program the page uses.</summary>
        Font = 3,

        /// <summary>An image the page draws.</summary>
        Image = 4,
    }

    /// <summary>The sample documents.</summary>
    public enum Sample
    {
        /// <summary>A plain one page document.</summary>
        Plain = 0,

        /// <summary>A form with a text field, check box and combo box.</summary>
        Form = 1,

        /// <summary>A form whose scripts are all recognised.</summary>
        CalculatedForm = 2,

        /// <summary>A document using everything that cannot be shown.</summary>
        Unsupported = 3,

        /// <summary>A form with comb fields.</summary>
        CombForm = 4,

        /// <summary>A flat form.</summary>
        FlatForm = 5,

        /// <summary>A page with typewriter text.</summary>
        FreeText = 6,

        /// <summary>A tagged document.</summary>
        Tagged = 7,

        /// <summary>A document with layers.</summary>
        Layers = 8,

        /// <summary>A document with an attached file.</summary>
        Attachment = 9,
    }

    /// <summary>Verifies XFA forms, scripts, media players and 3D models are found, and a plain document has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsUnsupportedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-unsupported-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithUnsupportedContent());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var check = DocumentFeature<IContentCheck>.Cast(document)!;
            await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.XfaForm | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(1)).IsEqualTo(UnsupportedContent.None);

            using var simple = new HyperPdfEngine().Open(plain, null);
            await Assert.That(DocumentFeature<IContentCheck>.Cast(simple)!.CheckDocument()).IsEqualTo(UnsupportedContent.None);
            await Assert.That(DocumentFeature<IContentCheck>.Cast(simple)!.CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        }
        finally
        {
            File.Delete(path);
            File.Delete(plain);
        }
    }

    /// <summary>A page whose content, form, font program or image is packed with a filter nobody knows is flagged, so the reader is told part of it is left out.</summary>
    /// <param name="part">Which part of the page uses the unknown filter.</param>
    /// <param name="expected">What the page is flagged for.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(UnknownFilterPart.Content, UnsupportedContent.UnreadableStreams)]
    [Arguments(UnknownFilterPart.Form, UnsupportedContent.UnreadableStreams)]
    [Arguments(UnknownFilterPart.Font, UnsupportedContent.UnreadableStreams)]
    [Arguments(UnknownFilterPart.Image, UnsupportedContent.UnreadableImages)]
    [Arguments(UnknownFilterPart.None, UnsupportedContent.None)]
    public async Task FlagsPartsPackedWithAnUnknownFilter(UnknownFilterPart part, UnsupportedContent expected)
    {
        using var test = new HyperPdfTestDocument(CreateUnknownFilterPage(part));

        await Assert.That(DocumentFeature<IContentCheck>.Cast(test.Document)!.CheckPage(0)).IsEqualTo(expected);
    }

    /// <summary>An image with an unknown filter is drawn as a plain placeholder, not as noise on black, because its bytes are not an image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsAPlaceholderForAnImageWithAnUnknownFilter()
    {
        using var test = new HyperPdfTestDocument(CreateUnknownFilterPage(UnknownFilterPart.Image));
        var pixels = new byte[PageSide * PageSide * BytesPerPixel];

        _ = test.Document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, PageSide, PageSide, PageSide * BytesPerPixel));

        var dark = 0;
        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            var pixel = pixels.AsSpan(i, BytesPerPixel);
            dark += pixel[..RgbBytes].IndexOfAnyExceptInRange((byte)0, (byte)(DarkLimit - 1)) < 0 ? 1 : 0;
        }

        await Assert.That(dark).IsZero();
    }

    /// <summary>The unknown filter notice names the parts left out, in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsThatPartsOfThePageAreLeftOut() =>
        await Assert.That(ContentWarnings.Describe(UnsupportedContent.UnreadableStreams)).IsEqualTo(
            "This document uses page parts packed with a compression it does not know, which HyperPDF cannot show or run. Those parts are left out of the page. "
            + "To use everything, open it in Adobe Acrobat Reader.");

    /// <summary>A form whose scripts are all ones that are run is not flagged for JavaScript.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisedFormScriptsAreNotFlagged()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateCalculatedForm());
        var check = DocumentFeature<IContentCheck>.Cast(test.Document)!;
        await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.None);
    }

    /// <summary>Pages outside each sample document have no unsupported content.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Plain)]
    [Arguments(Sample.Form)]
    [Arguments(Sample.CalculatedForm)]
    [Arguments(Sample.Unsupported)]
    [Arguments(Sample.CombForm)]
    [Arguments(Sample.FlatForm)]
    [Arguments(Sample.FreeText)]
    [Arguments(Sample.Tagged)]
    [Arguments(Sample.Layers)]
    [Arguments(Sample.Attachment)]
    public async Task MissingPagesHaveNoUnsupportedContent(Sample sample)
    {
        using var test = new HyperPdfTestDocument(CreateSample(sample));
        var check = DocumentFeature<IContentCheck>.Cast(test.Document)!;
        await Assert.That(check.CheckPage(MissingPage)).IsEqualTo(UnsupportedContent.None);
        await Assert.That(check.CheckPage(-1)).IsEqualTo(UnsupportedContent.None);
    }

    /// <summary>Makes a one page document in which one part uses a filter that does not exist.</summary>
    /// <param name="part">The part that uses the filter.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateUnknownFilterPage(UnknownFilterPart part)
    {
        const string unknown = "/Filter /NoSuchFilter";
        const string catalog = "<< /Type /Catalog /Pages 2 0 R >>";
        const string pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
        return part switch
        {
            UnknownFilterPart.Content => MiniPdf.Build(
                catalog,
                pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> /Contents 4 0 R >>",
                MiniPdf.Stream(unknown, "q Q")),
            UnknownFilterPart.Form => MiniPdf.Build(
                catalog,
                pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /Fm 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "/Fm Do"),
                MiniPdf.Stream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10] {unknown}", "q Q")),
            UnknownFilterPart.Font => MiniPdf.Build(
                catalog,
                pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "BT /F1 10 Tf (x) Tj ET"),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FontDescriptor 6 0 R >>",
                "<< /Type /FontDescriptor /FontName /Helvetica /Flags 32 /FontFile 7 0 R >>",
                MiniPdf.Stream(unknown, "x")),
            UnknownFilterPart.Image => MiniPdf.Build(
                catalog,
                pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /Im 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "q 100 0 0 100 0 0 cm /Im Do Q"),
                MiniPdf.Stream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 {unknown}", "x")),
            _ => MiniPdf.Build(
                catalog,
                pages,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "q Q")),
        };
    }

    /// <summary>Creates one of the sample documents.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateSample(Sample sample) => sample switch
    {
        Sample.Plain => TestPdf.Create(1),
        Sample.Form => TestPdf.CreateForm(),
        Sample.CalculatedForm => TestPdf.CreateCalculatedForm(),
        Sample.Unsupported => TestPdf.CreateWithUnsupportedContent(),
        Sample.CombForm => TestPdf.CreateCombForm(),
        Sample.FlatForm => TestPdf.CreateFlatForm(),
        Sample.FreeText => TestPdf.CreateWithFreeText(),
        Sample.Tagged => TestPdf.CreateTagged(),
        Sample.Layers => TestPdf.CreateWithLayers(),
        _ => TestPdf.CreateWithAttachment(),
    };
}
