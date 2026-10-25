// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for which annotations a flatten draws: the flags, the kind of annotation, popups, replies and missing appearances.</summary>
public sealed class FlattenSelectionTests
{
    /// <summary>The Hidden and Print flags together.</summary>
    private const int HiddenAndPrint = FlattenTestDocuments.Hidden | FlattenTestDocuments.Print;

    /// <summary>The NoView and Print flags together.</summary>
    private const int NoViewAndPrint = FlattenTestDocuments.NoView | FlattenTestDocuments.Print;

    /// <summary>The widgets, annotations and link in the mixed page.</summary>
    private const int MixedAnnotations = 3;

    /// <summary>The x of a pixel inside the square annotation.</summary>
    private const int AnnotationX = 30;

    /// <summary>The y of a pixel inside the square annotation, counted from the top of the page.</summary>
    private const int AnnotationY = 180;

    /// <summary>The colour of the square annotation's appearance.</summary>
    private static readonly Rgb Blue = new(0, 0, 255);

    /// <summary>Which annotations are drawn for each mode and flag value; the others stay.</summary>
    /// <param name="flags">The annotation's /F.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="drawn">Whether the annotation is drawn into the content.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FlattenTestDocuments.Print, PdfFlattenMode.Display, true)]
    [Arguments(0, PdfFlattenMode.Display, true)]
    [Arguments(HiddenAndPrint, PdfFlattenMode.Display, false)]
    [Arguments(NoViewAndPrint, PdfFlattenMode.Display, false)]
    [Arguments(FlattenTestDocuments.Print, PdfFlattenMode.Print, true)]
    [Arguments(NoViewAndPrint, PdfFlattenMode.Print, true)]
    [Arguments(0, PdfFlattenMode.Print, false)]
    [Arguments(HiddenAndPrint, PdfFlattenMode.Print, false)]
    public async Task FlagsDecideWhatIsDrawn(int flags, PdfFlattenMode mode, bool drawn)
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithAnnotation($"/F {flags}"), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], mode);

        await Assert.That(report.AnnotationsDrawn).IsEqualTo(drawn ? 1 : 0);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(drawn ? 0 : 1);
        await Assert.That(report.Skipped.Count).IsEqualTo(drawn ? 0 : 1);
    }

    /// <summary>A flatten for the screen matches the screen render, and a flatten for printing matches the printed render.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="flags">The render flags that match the mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfFlattenMode.Display, PdfRenderFlags.None)]
    [Arguments(PdfFlattenMode.Print, PdfRenderFlags.Printing)]
    public async Task EachModeMatchesItsRender(PdfFlattenMode mode, PdfRenderFlags flags)
    {
        var file = FlattenTestDocuments.WithAnnotation($"/F {NoViewAndPrint}");
        var before = FlattenTestDocuments.Render(file, flags);
        using var document = PdfDocumentReader.Open(file, null);
        _ = PdfDocumentFlattening.Flatten(document, [0], mode);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), flags);

        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));

        // The annotation has the NoView flag, so only the printed page shows it.
        await Assert.That(after.IsNear(AnnotationX, AnnotationY, Blue, 1)).IsEqualTo(mode == PdfFlattenMode.Print);
    }

    /// <summary>A hidden form field is removed with its form without being drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenWidgetLeavesWithItsForm()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithFieldKinds(HiddenAndPrint), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);

        await Assert.That(report.WidgetsDrawn).IsEqualTo(0);
        await Assert.That(report.WidgetsRemoved).IsGreaterThan(0);
        await Assert.That(report.FormRemoved).IsTrue();
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(report.Skipped.All(static skip => skip.Removed && skip.Reason == PdfFlattenSkipReason.Hidden)).IsTrue();
    }

    /// <summary>Each kind filter flattens only what it names.</summary>
    /// <param name="targets">The filter.</param>
    /// <param name="drawn">The number of annotations drawn.</param>
    /// <param name="remaining">The number of annotations left on the page.</param>
    /// <param name="formKept">Whether the form stays.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfFlattenTargets.All, 3, 0, false)]
    [Arguments(PdfFlattenTargets.WidgetsOnly, 1, 2, false)]
    [Arguments(PdfFlattenTargets.MarkupOnly, 1, 2, true)]
    [Arguments(PdfFlattenTargets.WithoutLinks, 2, 1, false)]
    public async Task KindFiltersFlattenOnlyWhatTheyName(PdfFlattenTargets targets, int drawn, int remaining, bool formKept)
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithMixedAnnotations(), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], new PdfFlattenOptions { Targets = targets });

        await Assert.That(report.TotalDrawn).IsEqualTo(drawn);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(remaining);
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsEqualTo(formKept);
        await Assert.That(drawn + remaining).IsEqualTo(MixedAnnotations);
    }

    /// <summary>A popup leaves with its annotation, and a reply that stays no longer points to it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PopupLeavesAndReplyLosesItsLink()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithPopupAndReply(), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var annotations = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!;

        await Assert.That(report.AnnotationsDrawn).IsEqualTo(1);
        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(annotations.GetDictionary(0)!.IsName(KnownName.Subtype, KnownName.Text)).IsTrue();
        await Assert.That(annotations.GetDictionary(0)!.ContainsKey(KnownName.IRT)).IsFalse();
        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.IsName(KnownName.Subtype, KnownName.Popup))).IsEqualTo(0);
    }

    /// <summary>An annotation without an appearance gets the shape a viewer draws for it, and the page looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationWithoutAppearanceIsDrawnLikeTheViewerDrawsIt()
    {
        var file = FlattenTestDocuments.WithHighlightWithoutAppearance();
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), PdfRenderFlags.None);

        await Assert.That(report.AppearancesDrawn).IsEqualTo(1);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
    }

    /// <summary>An annotation's optional content stays switchable: the drawing is inside the same group.</summary>
    /// <param name="visible">Whether the group is on by default.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OptionalContentStaysSwitchable(bool visible)
    {
        var file = FlattenTestDocuments.WithLayer(visible);
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), PdfRenderFlags.None);

        await Assert.That(FlattenTestDocuments.Content(document, 0)).Contains("/OC /HpFlatOc0 BDC");
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Properties)!.ContainsKey(document.Objects.Names.Intern("HpFlatOc0"))).IsTrue();
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
    }

    /// <summary>A trap network or a redaction that has not been applied stays, and the report says why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProductionMarksAndRedactionsStay()
    {
        var pdf = new RenderTestPdf(FlattenTestDocuments.Edge, FlattenTestDocuments.Edge);
        var redact = $"<< /Type /Annot /Subtype /Redact /Rect [10 10 50 30] /F {FlattenTestDocuments.Print} >>";
        var mark = $"<< /Type /Annot /Subtype /PrinterMark /Rect [60 10 90 30] /F {FlattenTestDocuments.Print} >>";
        pdf.PageEntries = $"/Annots [{redact} {mark}]";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        PdfFlattenSkipReason[] reasons = [PdfFlattenSkipReason.RedactionNotApplied, PdfFlattenSkipReason.ProductionMark];

        await Assert.That(report.TotalDrawn).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(reasons.Length);
        await Assert.That(report.Skipped.Select(static skip => skip.Reason)).IsEquivalentTo(reasons);
    }

    /// <summary>A form field with no appearance is drawn from its value, for every kind of field, and the form goes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldsWithoutAppearancesAreDrawnFromTheirValues()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        var widgets = FlattenTestDocuments.AnnotationCount(document, 0);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);

        await Assert.That(widgets).IsEqualTo(FormSamples.WidgetCount);
        await Assert.That(report.AppearancesDrawn).IsEqualTo(widgets);
        await Assert.That(report.WidgetsDrawn).IsEqualTo(widgets);
        await Assert.That(report.FormRemoved).IsTrue();
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.ContainsKey(KnownName.FT) || d.IsName(KnownName.Subtype, KnownName.Widget))).IsEqualTo(0);
    }

    /// <summary>An annotation's blend mode is set before its appearance is drawn, and its opacity entry is left to the appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationBlendModeIsAppliedAndOpacityIsNot()
    {
        var file = FlattenTestDocuments.WithAnnotation("/F 4 /BM /Multiply /CA 0.5");
        using var document = PdfDocumentReader.Open(file, null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var content = FlattenTestDocuments.Content(document, 0);

        await Assert.That(content).Contains("HpFlatGs0 gs");
        await Assert.That(content).DoesNotContain("0.5");
        var states = PdfDocumentPages.GetPage(document, 0).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.ExtGState)!;
        await Assert.That(states.GetDictionary(document.Objects.Names.Intern("HpFlatGs0"u8))!.IsName(KnownName.BM, KnownName.Multiply)).IsTrue();
    }

    /// <summary>A document that claims PDF/A-1 keeps an annotation in an optional content group, which PDF/A-1 does not allow in a drawing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA1ClaimKeepsAnAnnotationInALayer()
    {
        var pdf = ConformancePdf.Page();
        var square = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 40 20]", "0 0 1 rg 0 0 40 20 re f");
        var group = pdf.AddObject("<< /Type /OCG /Name (Notes) >>");
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{group} 0 R] /D << /ON [{group} 0 R] >> >>";
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F 4 /OC {group} 0 R /AP << /N {square} 0 R >> >>]";
        ConformancePdf.Claim(pdf, 1, "B", null);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);

        await Assert.That(report.TotalDrawn).IsEqualTo(0);
        await Assert.That(report.Skipped[0].Reason).IsEqualTo(PdfFlattenSkipReason.LayerNotAllowed);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(1);
    }
}
