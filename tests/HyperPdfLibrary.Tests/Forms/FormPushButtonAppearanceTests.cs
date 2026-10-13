// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests push button icons and captions: the text position <c>/TP</c> and icon fit <c>/IF</c> of the appearance characteristics (ISO 32000-2 12.5.6.19).</summary>
public sealed class FormPushButtonAppearanceTests
{
    /// <summary>The push button flag.</summary>
    private const string PushButton = "/FT /Btn /Ff 65536 /T (Go)";

    /// <summary>How close two positions must be, in points.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The width of the widget, 20 to 220.</summary>
    private const float WidgetWidth = 200;

    /// <summary>The height of the widget, 200 to 230.</summary>
    private const float WidgetHeight = 30;

    /// <summary>The border width the tests use.</summary>
    private const float Border = 1;

    /// <summary>The border on both sides of the widget.</summary>
    private const float BothBorders = Border + Border;

    /// <summary>The width of the icon form.</summary>
    private const float IconWidth = 100;

    /// <summary>The height of the icon form.</summary>
    private const float IconHeight = 50;

    /// <summary>The text size of the default appearance.</summary>
    private const float TextSize = 12;

    /// <summary>The thousandths in an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Half, to centre.</summary>
    private const float Half = 0.5F;

    /// <summary>The numbers of a matrix.</summary>
    private const int MatrixNumbers = 6;

    /// <summary>The position of the x offset in a matrix.</summary>
    private const int OffsetXSlot = 4;

    /// <summary>The position of the y offset in a matrix.</summary>
    private const int OffsetYSlot = 5;

    /// <summary>The position of the vertical scale in a matrix.</summary>
    private const int ScaleYSlot = 3;

    /// <summary>The icon the fit tests use is this much wider than the space.</summary>
    private const float SmallSpace = 50;

    /// <summary>A small icon width.</summary>
    private const float SmallIconWidth = 20;

    /// <summary>A small icon height.</summary>
    private const float SmallIconHeight = 10;

    /// <summary>The factor that fits the small icon to the small space.</summary>
    private const float GrowScale = 2.5F;

    /// <summary>With the caption below the icon, the icon is fitted above it, in proportion, and centred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaptionBelowLeavesTheIconTheSpaceAboveIt()
    {
        using var document = Open("/MK << /CA (Push) /TP 2 /I 7 0 R /BC [0 0 0] >>");

        var drawn = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var content = FormTestPdf.Content(document);
        var matrix = IconMatrix(content);
        const float areaWidth = WidgetWidth - BothBorders;
        var areaHeight = WidgetHeight - BothBorders - CaptionHeight();
        var scale = Math.Min(areaWidth / IconWidth, areaHeight / IconHeight);

        await Assert.That(drawn).IsTrue();
        await Assert.That(content).Contains("(Push) Tj");
        await Assert.That(content).Contains("/Icon Do");
        await Assert.That(matrix[0]).IsEqualTo(matrix[ScaleYSlot]).Within(Tolerance);
        await Assert.That(matrix[0]).IsEqualTo(scale).Within(Tolerance);
        await Assert.That(matrix[OffsetXSlot]).IsEqualTo(Border + ((areaWidth - (IconWidth * scale)) * Half)).Within(Tolerance);
        await Assert.That(matrix[OffsetYSlot]).IsEqualTo(Border + CaptionHeight() + ((areaHeight - (IconHeight * scale)) * Half)).Within(Tolerance);
    }

    /// <summary>With the caption above the icon, the icon sits at the bottom and the caption is drawn in the strip above it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaptionAboveLeavesTheIconTheSpaceBelowIt()
    {
        using var document = Open("/MK << /CA (Push) /TP 3 /I 7 0 R /BC [0 0 0] >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var matrix = IconMatrix(FormTestPdf.Content(document));
        var areaHeight = WidgetHeight - BothBorders - CaptionHeight();
        var scale = Math.Min((WidgetWidth - BothBorders) / IconWidth, areaHeight / IconHeight);

        await Assert.That(matrix[0]).IsEqualTo(scale).Within(Tolerance);
        await Assert.That(matrix[OffsetYSlot]).IsEqualTo(Border + ((areaHeight - (IconHeight * scale)) * Half)).Within(Tolerance);
    }

    /// <summary>With only the icon shown, it takes the whole inside of the border and the caption is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconOnlyIgnoresTheCaption()
    {
        using var document = Open("/MK << /CA (Push) /TP 1 /I 7 0 R /BC [0 0 0] >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var content = FormTestPdf.Content(document);

        await Assert.That(content).DoesNotContain("Tj");
        await Assert.That(IconMatrix(content)[0]).IsEqualTo((WidgetHeight - BothBorders) / IconHeight).Within(Tolerance);
    }

    /// <summary>With only the caption shown, no icon is drawn and the appearance has no XObject resources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaptionOnlyDrawsNoIcon()
    {
        using var document = Open("/MK << /CA (Push) /TP 0 /I 7 0 R /BC [0 0 0] >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var resources = FormTestPdf.Stream(document).Dictionary.GetDictionary(KnownName.Resources)!;

        await Assert.That(FormTestPdf.Content(document)).DoesNotContain(" Do");
        await Assert.That(resources.ContainsKey(KnownName.XObject)).IsFalse();
    }

    /// <summary>An icon fit that never scales draws the icon at its own size, placed by the alignment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverScaleKeepsTheIconSizeAndAlignsIt()
    {
        using var document = Open("/MK << /TP 1 /I 7 0 R /BC [0 0 0] /IF << /SW /N /A [0 0] >> >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var matrix = IconMatrix(FormTestPdf.Content(document));

        await Assert.That(matrix[0]).IsEqualTo(1F).Within(Tolerance);
        await Assert.That(matrix[OffsetXSlot]).IsEqualTo(Border).Within(Tolerance);
    }

    /// <summary>Anamorphic scaling stretches the icon to fill the space in both directions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnamorphicScalingFillsTheSpace()
    {
        using var document = Open("/MK << /TP 1 /I 7 0 R /BC [0 0 0] /IF << /SW /A /S /A >> >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var matrix = IconMatrix(FormTestPdf.Content(document));

        await Assert.That(matrix[0]).IsEqualTo((WidgetWidth - BothBorders) / IconWidth).Within(Tolerance);
        await Assert.That(matrix[ScaleYSlot]).IsEqualTo((WidgetHeight - BothBorders) / IconHeight).Within(Tolerance);
    }

    /// <summary>The icon is shared with the appearance by reference, not copied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconIsReferencedFromTheAppearanceResources()
    {
        using var document = Open("/MK << /TP 1 /I 7 0 R >>");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var icons = FormTestPdf.Stream(document).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.XObject)!;

        await Assert.That(icons.GetRaw(document.Objects.Names.Intern("Icon")).IsReference).IsTrue();
    }

    /// <summary>The icon fit places an icon by the rules of ISO 32000-2 Table 190.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconFitFollowsTheScaleRules()
    {
        var tooBig = new FormIconFit(FormIconScale.WhenTooBig, true, Half, Half, false);
        var tooSmall = new FormIconFit(FormIconScale.WhenTooSmall, true, Half, Half, false);

        await Assert.That(tooBig.Place(IconWidth, IconHeight, SmallSpace, SmallSpace).ScaleX).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(tooBig.Place(SmallIconWidth, SmallIconHeight, SmallSpace, SmallSpace).ScaleX).IsEqualTo(1F).Within(Tolerance);
        await Assert.That(tooSmall.Place(SmallIconWidth, SmallIconHeight, SmallSpace, SmallSpace).ScaleX).IsEqualTo(GrowScale).Within(Tolerance);
        await Assert.That(tooSmall.Place(IconWidth, IconHeight, SmallSpace, SmallSpace).ScaleX).IsEqualTo(1F).Within(Tolerance);
    }

    /// <summary>Opens a form with a push button and an icon form as object 7.</summary>
    /// <param name="extra">More widget entries.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string extra) => FormTestPdf.Open(
        FormTestPdf.Helvetica,
        $"{PushButton} {extra}",
        MiniPdf.Stream(string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Form /BBox [0 0 {IconWidth} {IconHeight}]"), "1 0 0 rg 0 0 100 50 re f"));

    /// <summary>Gets the height of a line of caption text in the field's font.</summary>
    /// <returns>The height in points.</returns>
    private static float CaptionHeight()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, "/FT /Tx /T (x)");
        var font = FormFont.CreateFallback(document.Objects);
        return (font.Ascent - font.Descent) * TextSize / GlyphUnits;
    }

    /// <summary>Reads the matrix drawn just before the icon.</summary>
    /// <param name="content">The appearance content.</param>
    /// <returns>The six numbers.</returns>
    private static float[] IconMatrix(string content)
    {
        var at = content.IndexOf("/Icon Do", StringComparison.Ordinal);
        var lineEnd = content.LastIndexOf('\n', at - 1);
        var lineStart = content.LastIndexOf('\n', lineEnd - 1) + 1;
        var numbers = content[lineStart..lineEnd].Replace(" cm", string.Empty, StringComparison.Ordinal).Split(' ');
        var matrix = new float[MatrixNumbers];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = float.Parse(numbers[i], CultureInfo.InvariantCulture);
        }

        return matrix;
    }
}
