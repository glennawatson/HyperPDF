// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Tests for the PDF/A rule that a page drawing with transparency, in a file without a PDF/A output intent, defines a
/// blending colour space. Each case was checked against veraPDF 1.30.3, which gives the same pass or fail.
/// </summary>
public sealed class PdfAPageTransparencyRulesTests
{
    /// <summary>The PDF/A-2 rule, clause 6.2.10 test 2.</summary>
    private const string PdfA2Rule = "6.2.10:2";

    /// <summary>The PDF/A-4 rule, clause 6.2.9 test 2.</summary>
    private const string PdfA4Rule = "6.2.9:2";

    /// <summary>A graphics state with a fill alpha of one half.</summary>
    private const string HalfFillAlpha = "/ExtGState << /G << /ca 0.5 >> >>";

    /// <summary>A graphics state counts only when something is painted with it, and the alpha must match the paint.</summary>
    /// <param name="graphicsState">The graphics state entries.</param>
    /// <param name="content">The page content.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/ca 0.5", "/G gs 0 0 10 10 re f", true)]
    [Arguments("/ca 0.5", "/G gs", false)]
    [Arguments("/ca 0.5", "/G gs 0 0 10 10 re n", false)]
    [Arguments("/ca 0.5", "/G gs 0 0 10 10 re S", false)]
    [Arguments("/CA 0.5", "/G gs 0 0 10 10 re S", true)]
    [Arguments("/ca 0.5", "q /G gs Q 0 0 10 10 re f", false)]
    [Arguments("/BM /Multiply", "/G gs 0 0 10 10 re S", true)]
    [Arguments("/BM /Compatible", "/G gs 0 0 10 10 re f", true)]
    [Arguments("/SMask /None", "/G gs 0 0 10 10 re f", false)]
    public async Task PaintedGraphicsState(string graphicsState, string content, bool fails)
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = $"/ExtGState << /G << {graphicsState} >> >>";
        pdf.Content = content;

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, PdfA2Rule)).IsEqualTo(fails);
    }

    /// <summary>A transparency group form counts whatever it draws, and an image counts when it has a soft mask.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupsAndSoftMasks()
    {
        var group = ConformancePdf.Page();
        var form = group.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Group << /S /Transparency >>", "0 0 10 10 re f");
        group.Resources = $"/XObject << /X {form} 0 R >>";
        group.Content = "/X Do";
        var masked = ConformancePdf.Page();
        var mask = masked.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", "0");
        var image = masked.AddStream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /SMask {mask} 0 R", "0");
        masked.Resources = $"/XObject << /I {image} 0 R >>";
        masked.Content = "/I Do";

        await Assert.That(Fails(group, PdfALevel.PdfA2B, PdfA2Rule)).IsTrue();
        await Assert.That(Fails(masked, PdfALevel.PdfA2B, PdfA2Rule)).IsTrue();
    }

    /// <summary>A page group with a blending colour space, or a PDF/A output intent, satisfies the rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendingColourSpaceOrIntentPasses()
    {
        var group = TransparentPage();
        group.PageEntries = "/Group << /S /Transparency /CS /DeviceRGB >>";
        var intent = TransparentPage();
        ConformancePdf.Intent(intent, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);
        var groupWithoutSpace = TransparentPage();
        groupWithoutSpace.PageEntries = "/Group << /S /Transparency >>";

        await Assert.That(Fails(group, PdfALevel.PdfA2B, PdfA2Rule)).IsFalse();
        await Assert.That(Fails(intent, PdfALevel.PdfA2B, PdfA2Rule)).IsFalse();
        await Assert.That(Fails(groupWithoutSpace, PdfALevel.PdfA2B, PdfA2Rule)).IsTrue();
        await Assert.That(Fails(groupWithoutSpace, PdfALevel.PdfA4, PdfA4Rule)).IsTrue();
    }

    /// <summary>An annotation appearance drawing with transparency makes its page transparent, in any appearance state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationAppearances()
    {
        var pdf = ConformancePdf.Page();
        var plain = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", "0 0 10 10 re f");
        var down = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << {HalfFillAlpha} >>", "/G gs 0 0 10 10 re f");
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [0 0 10 10] /F 4 /AP << /N {plain} 0 R /D {down} 0 R >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, PdfA2Rule)).IsTrue();
    }

    /// <summary>A Type 3 glyph procedure counts only when the text shows that glyph.</summary>
    /// <param name="text">The shown string; code 97 maps to the glyph drawing with transparency.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("(a)", true)]
    [Arguments("(b)", false)]
    public async Task Type3GlyphsShown(string text, bool fails)
    {
        var pdf = ConformancePdf.Page();
        var transparent = pdf.AddStream(string.Empty, "1000 0 d0 /G gs 0 0 500 500 re f");
        var opaque = pdf.AddStream(string.Empty, "1000 0 d0 0 0 500 500 re f");
        var font = pdf.AddObject(
            "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /FirstChar 97 /LastChar 98 /Widths [1000 1000] "
            + $"/Encoding << /Differences [97 /square /triangle] >> /CharProcs << /square {transparent} 0 R /triangle {opaque} 0 R >> /Resources << {HalfFillAlpha} >> >>");
        pdf.Resources = $"/Font << /F {font} 0 R >>";
        pdf.Content = $"BT /F 12 Tf {text} Tj ET";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, PdfA2Rule)).IsEqualTo(fails);
    }

    /// <summary>Validation stops when its token is cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationHonoursCancellation()
    {
        using var document = PdfDocumentReader.Open(TransparentPage().ToBytes(), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => await PdfAValidator.ValidateAsync(document, PdfALevel.PdfA2B, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Builds a page that fills with a fill alpha of one half.</summary>
    /// <returns>The builder.</returns>
    private static RenderTestPdf TransparentPage()
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = HalfFillAlpha;
        pdf.Content = "/G gs 0 0 10 10 re f";
        return pdf;
    }

    /// <summary>Validates a PDF and determines whether a rule failed.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="level">The level.</param>
    /// <param name="rule">The clause:test id.</param>
    /// <returns><see langword="true"/> when it failed.</returns>
    private static bool Fails(RenderTestPdf pdf, PdfALevel level, string rule)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        return PdfAValidator.Validate(document, level).Failed.Any(result => $"{result.Clause}:{result.Test}" == rule);
    }
}
