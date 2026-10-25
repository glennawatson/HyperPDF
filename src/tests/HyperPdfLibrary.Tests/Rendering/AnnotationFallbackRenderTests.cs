// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders the Link, Caret and Redact annotations that have no appearance stream, and checks that a real appearance and the annotation flags still win.</summary>
public sealed class AnnotationFallbackRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 10;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The device column of a 4 point border drawn inside x 20.</summary>
    private const int BorderColumn = 22;

    /// <summary>The device column of a one point outline drawn inside x 20.</summary>
    private const int OutlineColumn = 20;

    /// <summary>The device row of the bottom border, a 4 point line centred 2 points above y 20.</summary>
    private const int BottomBorderRow = 78;

    /// <summary>The device row inside the caret's notched base, where the caret is empty.</summary>
    private const int NotchRow = 75;

    /// <summary>The device row, in the lower part of the caret, near its foot.</summary>
    private const int FootRow = 66;

    /// <summary>The device column near the left foot of the caret.</summary>
    private const int FootColumn = 24;

    /// <summary>The device row above a caret whose top is 30 points inside the rectangle.</summary>
    private const int AboveCaretRow = 30;

    /// <summary>The device row inside that shorter caret.</summary>
    private const int InsideCaretRow = 60;

    /// <summary>The entries of a ten point appearance stream.</summary>
    private const string SmallForm = "/Type /XObject /Subtype /Form /BBox [0 0 10 10]";

    /// <summary>The content of an appearance that fills its box green.</summary>
    private const string GreenFill = "0 1 0 rg 0 0 10 10 re f";

    /// <summary>The first device row of the link's left edge.</summary>
    private const int EdgeTop = 20;

    /// <summary>The device row after the last of the link's left edge.</summary>
    private const int EdgeBottom = 80;

    /// <summary>A channel at half strength over white.</summary>
    private const int Half = 128;

    /// <summary>A link with a 4 point red border.</summary>
    private const string RedLink = "/Subtype /Link /Rect [20 20 80 80] /C [1 0 0] /Border [0 0 4]";

    /// <summary>A caret filled with blue.</summary>
    private const string BlueCaret = "/Subtype /Caret /Rect [20 20 80 80] /C [0 0 1]";

    /// <summary>A redaction annotation with every entry that describes the result of applying it.</summary>
    private const string Redaction = "/Subtype /Redact /Rect [20 20 80 80] /C [0 0 1] /IC [0 0 0] /OverlayText (REDACTED) /DA (/Helv 12 Tf 1 g)";

    /// <summary>A link with a /Border width draws a rectangle border inside its rectangle and leaves the inside clear.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkBorderIsDrawnInsideTheRectangle()
    {
        var image = RenderAnnotation($"{RedLink} /F 4", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.Red255, Tolerance, nameof(LinkBorderIsDrawnInsideTheRectangle));
        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(LinkBorderIsDrawnInsideTheRectangle));
    }

    /// <summary>A link with neither /Border nor /BS draws nothing, as Acrobat does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkWithoutBorderEntriesIsInvisible()
    {
        var image = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /C [1 0 0]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.White, Tolerance, nameof(LinkWithoutBorderEntriesIsInvisible));
    }

    /// <summary>A zero width border, or an empty /Border array, draws nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkWithZeroWidthBorderIsInvisible()
    {
        var zero = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /Border [0 0 0]", PdfRenderFlags.Annotations);
        var empty = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /Border []", PdfRenderFlags.Annotations);
        var style = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /BS << /W 0 >>", PdfRenderFlags.Annotations);

        await RenderCheck.Near(zero, OutlineColumn, Middle, Rgb.White, Tolerance, nameof(LinkWithZeroWidthBorderIsInvisible));
        await RenderCheck.Near(empty, OutlineColumn, Middle, Rgb.White, Tolerance, nameof(LinkWithZeroWidthBorderIsInvisible));
        await RenderCheck.Near(style, OutlineColumn, Middle, Rgb.White, Tolerance, nameof(LinkWithZeroWidthBorderIsInvisible));
    }

    /// <summary>A link whose colour array is empty is transparent, so its border is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkWithEmptyColorIsInvisible()
    {
        var image = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /C [] /Border [0 0 4]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.White, Tolerance, nameof(LinkWithEmptyColorIsInvisible));
    }

    /// <summary>A link border is black when /C is absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkBorderDefaultsToBlack()
    {
        var image = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /BS << /W 4 >>", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.Black, Tolerance, nameof(LinkBorderDefaultsToBlack));
    }

    /// <summary>The underline border style draws only the bottom edge.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkUnderlineStyleDrawsTheBottomEdge()
    {
        var image = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /C [1 0 0] /BS << /W 4 /S /U >>", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, BottomBorderRow, Rgb.Red255, Tolerance, nameof(LinkUnderlineStyleDrawsTheBottomEdge));
        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.White, Tolerance, nameof(LinkUnderlineStyleDrawsTheBottomEdge));
    }

    /// <summary>A dashed border leaves gaps along its edge.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkDashedStyleLeavesGaps()
    {
        var image = RenderAnnotation("/Subtype /Link /Rect [20 20 80 80] /C [1 0 0] /BS << /W 4 /S /D /D [10 10] >>", PdfRenderFlags.Annotations);
        var seen = 0;
        var missed = 0;
        for (var row = EdgeTop; row < EdgeBottom; row++)
        {
            if (image.IsNear(BorderColumn, row, Rgb.Red255, Tolerance))
            {
                seen++;
            }
            else
            {
                missed++;
            }
        }

        await Assert.That(seen).IsGreaterThan(0);
        await Assert.That(missed).IsGreaterThan(0);
    }

    /// <summary>A real appearance stream replaces the generated link border.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkAppearanceStreamWins()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var green = pdf.AddStream(SmallForm, GreenFill);
        var annotation = pdf.AddObject($"<< /Type /Annot {RedLink} /AP << /N {green} 0 R >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.Green255, Tolerance, nameof(LinkAppearanceStreamWins));
    }

    /// <summary>A caret is a filled symbol: solid at the middle, clear in the notch at its base and clear in the corners.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretIsFilledWithItsColor()
    {
        var image = RenderAnnotation(BlueCaret, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Blue255, Tolerance, nameof(CaretIsFilledWithItsColor));
        await RenderCheck.Near(image, Middle, NotchRow, Rgb.White, Tolerance, nameof(CaretIsFilledWithItsColor));
        await RenderCheck.Near(image, OutlineColumn + 1, OutlineColumn + 1, Rgb.White, Tolerance, nameof(CaretIsFilledWithItsColor));
    }

    /// <summary>A caret is black when /C is absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretDefaultsToBlack()
    {
        var image = RenderAnnotation("/Subtype /Caret /Rect [20 20 80 80]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Black, Tolerance, nameof(CaretDefaultsToBlack));
    }

    /// <summary>The /RD differences shrink the caret away from the left, top, right and bottom of /Rect, in that order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretKeepsInsideItsDifferences()
    {
        var image = RenderAnnotation("/Subtype /Caret /Rect [20 20 80 80] /C [0 0 1] /RD [0 30 0 0]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, AboveCaretRow, Rgb.White, Tolerance, nameof(CaretKeepsInsideItsDifferences));
        await RenderCheck.Near(image, Middle, InsideCaretRow, Rgb.Blue255, Tolerance, nameof(CaretKeepsInsideItsDifferences));
    }

    /// <summary>A real appearance stream replaces the generated caret.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretAppearanceStreamWins()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var green = pdf.AddStream(SmallForm, GreenFill);
        var annotation = pdf.AddObject($"<< /Type /Annot {BlueCaret} /AP << /N {green} 0 R >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, FootColumn, FootRow, Rgb.Green255, Tolerance, nameof(CaretAppearanceStreamWins));
    }

    /// <summary>The redaction preview outlines the region and does not fill it, so the content below stays readable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactPreviewOutlinesWithoutFilling()
    {
        var image = RenderAnnotation(Redaction, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, OutlineColumn, Middle, Rgb.Blue255, Tolerance, nameof(RedactPreviewOutlinesWithoutFilling));
        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(RedactPreviewOutlinesWithoutFilling));
    }

    /// <summary>The preview does not apply the redaction: page content under the region is still drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactPreviewLeavesTheContentBelowVisible()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "0 1 0 rg 0 0 100 100 re f" };
        var annotation = pdf.AddObject($"<< /Type /Annot {Redaction} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Green255, Tolerance, nameof(RedactPreviewLeavesTheContentBelowVisible));
        await RenderCheck.Near(image, OutlineColumn, Middle, Rgb.Blue255, Tolerance, nameof(RedactPreviewLeavesTheContentBelowVisible));
    }

    /// <summary>The preview is red when the annotation has no colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactPreviewDefaultsToRed()
    {
        var image = RenderAnnotation("/Subtype /Redact /Rect [20 20 80 80]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, OutlineColumn, Middle, Rgb.Red255, Tolerance, nameof(RedactPreviewDefaultsToRed));
    }

    /// <summary>The preview follows /QuadPoints rather than /Rect when both are present.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactPreviewFollowsQuadPoints()
    {
        var image = RenderAnnotation("/Subtype /Redact /Rect [0 0 100 100] /C [0 0 1] /QuadPoints [20 60 80 60 20 40 80 40]", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, OutlineColumn, Middle, Rgb.Blue255, Tolerance, nameof(RedactPreviewFollowsQuadPoints));
        await RenderCheck.Near(image, Middle, 0, Rgb.White, Tolerance, nameof(RedactPreviewFollowsQuadPoints));
        await RenderCheck.Near(image, 0, Middle, Rgb.White, Tolerance, nameof(RedactPreviewFollowsQuadPoints));
    }

    /// <summary>A real appearance stream replaces the generated redaction preview.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactAppearanceStreamWins()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var green = pdf.AddStream(SmallForm, GreenFill);
        var annotation = pdf.AddObject($"<< /Type /Annot {Redaction} /AP << /N {green} 0 R >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Green255, Tolerance, nameof(RedactAppearanceStreamWins));
    }

    /// <summary>Hidden annotations are never drawn, whatever their subtype.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenFlagHidesTheFallbackSubtypes()
    {
        foreach (var entries in new[] { RedLink, BlueCaret, Redaction })
        {
            var image = RenderAnnotation($"{entries} /F 2", PdfRenderFlags.Annotations | PdfRenderFlags.Printing);
            var screen = RenderAnnotation($"{entries} /F 2", PdfRenderFlags.Annotations);

            await RenderCheck.Near(image, BorderColumn, Middle, Rgb.White, Tolerance, nameof(HiddenFlagHidesTheFallbackSubtypes));
            await RenderCheck.Near(screen, BorderColumn, Middle, Rgb.White, Tolerance, nameof(HiddenFlagHidesTheFallbackSubtypes));
            await RenderCheck.Near(screen, OutlineColumn, Middle, Rgb.White, Tolerance, nameof(HiddenFlagHidesTheFallbackSubtypes));
        }
    }

    /// <summary>NoView leaves the generated link off the screen but prints it with the Print flag; without Print it never prints.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoViewAndPrintFlagsApplyToGeneratedLinks()
    {
        var onScreen = RenderAnnotation($"{RedLink} /F 36", PdfRenderFlags.Annotations);
        var printed = RenderAnnotation($"{RedLink} /F 36", PdfRenderFlags.Annotations | PdfRenderFlags.Printing);
        var unprinted = RenderAnnotation($"{RedLink} /F 0", PdfRenderFlags.Annotations | PdfRenderFlags.Printing);

        await RenderCheck.Near(onScreen, BorderColumn, Middle, Rgb.White, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedLinks));
        await RenderCheck.Near(printed, BorderColumn, Middle, Rgb.Red255, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedLinks));
        await RenderCheck.Near(unprinted, BorderColumn, Middle, Rgb.White, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedLinks));
    }

    /// <summary>NoView and Print apply to the generated caret and redaction preview in the same way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoViewAndPrintFlagsApplyToGeneratedCaretsAndRedactions()
    {
        var caretScreen = RenderAnnotation($"{BlueCaret} /F 36", PdfRenderFlags.Annotations);
        var caretPrinted = RenderAnnotation($"{BlueCaret} /F 36", PdfRenderFlags.Annotations | PdfRenderFlags.Printing);
        var redactScreen = RenderAnnotation($"{Redaction} /F 36", PdfRenderFlags.Annotations);
        var redactPrinted = RenderAnnotation($"{Redaction} /F 36", PdfRenderFlags.Annotations | PdfRenderFlags.Printing);

        await RenderCheck.Near(caretScreen, Middle, Middle, Rgb.White, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedCaretsAndRedactions));
        await RenderCheck.Near(caretPrinted, Middle, Middle, Rgb.Blue255, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedCaretsAndRedactions));
        await RenderCheck.Near(redactScreen, OutlineColumn, Middle, Rgb.White, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedCaretsAndRedactions));
        await RenderCheck.Near(redactPrinted, OutlineColumn, Middle, Rgb.Blue255, Tolerance, nameof(NoViewAndPrintFlagsApplyToGeneratedCaretsAndRedactions));
    }

    /// <summary>The /CA opacity applies to the generated link border.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityAppliesToTheLinkBorder()
    {
        var image = RenderAnnotation($"{RedLink} /CA 0.5", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, BorderColumn, Middle, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(OpacityAppliesToTheLinkBorder));
    }

    /// <summary>Renders a page with one annotation.</summary>
    /// <param name="entries">The annotation's entries other than /Type.</param>
    /// <param name="flags">The render flags.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderAnnotation(string entries, PdfRenderFlags flags)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {entries} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage(1, 0, flags);
    }
}
