// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the link, form field and annotation findings.</summary>
[NotInParallel]
public sealed class AccessibilityAnnotationTests
{
    /// <summary>The page entry that makes the tab order follow the structure.</summary>
    private const string Tabs = "/Tabs /S";

    /// <summary>A widget with a label.</summary>
    private const string LabelledWidget = "/Subtype /Widget /FT /Tx /T (name) /TU (Your name) /Rect [72 500 200 520]";

    /// <summary>A tagged link with contents is clean.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedLinkWithContentsIsClean()
    {
        var report = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddLink(pdf, document, "/Contents (Go)", string.Empty)));

        await Assert.That(report.Findings.Count).IsEqualTo(0);
        await Assert.That(report.Pages[0].AnnotationCount).IsEqualTo(1);
    }

    /// <summary>A link outside the tree is flagged as untagged, and as undescribed when it has no contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedLinkIsFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) =>
        {
            _ = AccessibilityPdfs.AddLink(pdf, string.Empty);
            return AccessibilityPdfs.DefaultLayout(pdf, document);
        }));

        await Assert.That(report.GetCount(PdfAccessibilityCode.LinkNotTagged)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.LinkNoDescription)).IsEqualTo(1);
        await Assert.That(report.Findings[0].ElementId).IsNull();
    }

    /// <summary>A link element's <c>/Alt</c> describes the link when the annotation has no contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkAltTextDescribesTheLink()
    {
        var described = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddLink(pdf, document, string.Empty, "/Alt (Go)")));
        var bare = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddLink(pdf, document, string.Empty, string.Empty)));

        await Assert.That(described.Findings.Count).IsEqualTo(0);
        await Assert.That(bare.GetCount(PdfAccessibilityCode.LinkNoDescription)).IsEqualTo(1);
        await Assert.That(bare.Findings[0].ElementId).IsNotNull();
    }

    /// <summary>A labelled widget in a Form element is clean.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedLabelledWidgetIsClean()
    {
        var report = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddWidget(pdf, document, LabelledWidget, "Form")));

        await Assert.That(report.Findings.Count).IsEqualTo(0);
    }

    /// <summary>A widget with no label, or outside the tree, is flagged; a label on the parent field counts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidgetProblemsAreFlagged()
    {
        var noLabel = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddWidget(pdf, document, "/Subtype /Widget /FT /Tx /T (n) /Rect [72 500 200 520]", "Form")));
        var untagged = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) =>
        {
            _ = AccessibilityPdfs.AddAnnotation(pdf, LabelledWidget);
            return AccessibilityPdfs.DefaultLayout(pdf, document);
        }));
        var inherited = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) =>
        {
            var field = pdf.Add("<< /FT /Tx /T (n) /TU (Parent label) >>");
            return AddWidget(pdf, document, $"/Subtype /Widget /Parent {field} 0 R /Rect [72 500 200 520]", "Form");
        }));
        var wrongType = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddWidget(pdf, document, LabelledWidget, "Span")));

        await Assert.That(noLabel.GetCount(PdfAccessibilityCode.FormFieldNoLabel)).IsEqualTo(1);
        await Assert.That(untagged.GetCount(PdfAccessibilityCode.FormFieldNotTagged)).IsEqualTo(1);
        await Assert.That(inherited.Findings.Count).IsEqualTo(0);
        await Assert.That(wrongType.GetCount(PdfAccessibilityCode.FormFieldNotTagged)).IsEqualTo(1);
    }

    /// <summary>Other annotations must be in the tree, as Annot for UA-1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherAnnotationsNeedAnAnnotElement()
    {
        const string note = "/Subtype /Text /Contents (Note) /Rect [72 400 90 418]";
        var tagged = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddObject(pdf, document, note, "Annot")));
        var untagged = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) =>
        {
            _ = AccessibilityPdfs.AddAnnotation(pdf, note);
            return AccessibilityPdfs.DefaultLayout(pdf, document);
        }));
        var wrongType = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) => AddObject(pdf, document, note, "Span")));
        var part2 = await AccessibilityPdfs.ReportAsync(
            Spec(static (pdf, document) => AddObject(pdf, document, note, "Span")) with { Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true) });

        await Assert.That(tagged.Findings.Count).IsEqualTo(0);
        await Assert.That(untagged.GetCount(PdfAccessibilityCode.AnnotationNotTagged)).IsEqualTo(1);
        await Assert.That(wrongType.GetCount(PdfAccessibilityCode.AnnotationWrongStructureType)).IsEqualTo(1);
        await Assert.That(part2.Findings.Count(static finding => finding.Code == PdfAccessibilityCode.AnnotationWrongStructureType)).IsEqualTo(0);
    }

    /// <summary>With no structure tree, only the checks that need no tags run on annotations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentSkipsTaggingChecks()
    {
        var report = await AccessibilityPdfs.ReportAsync(Spec(static (pdf, document) =>
        {
            _ = AccessibilityPdfs.AddLink(pdf, "/Contents (Go)");
            _ = AccessibilityPdfs.AddAnnotation(pdf, "/Subtype /Text /Rect [72 400 90 418]");
            return AccessibilityPdfs.DefaultLayout(pdf, document);
        }) with { Tree = false });

        await Assert.That(report.GetCount(PdfAccessibilityCode.LinkNotTagged)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.AnnotationNotTagged)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoStructureTree)).IsEqualTo(1);
    }

    /// <summary>Makes a spec with tab order set, so the page never adds a tab finding.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The spec.</returns>
    private static AccessibilitySpec Spec(Func<TaggedPdfBuilder, int, string> layout) => new() { PageExtra = Tabs, Layout = layout };

    /// <summary>Adds the default elements, a link annotation and a Link element that refers to it.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element.</param>
    /// <param name="annotation">Extra annotation entries.</param>
    /// <param name="element">Extra element entries.</param>
    /// <returns>The kids.</returns>
    private static string AddLink(TaggedPdfBuilder pdf, int document, string annotation, string element)
    {
        var link = AccessibilityPdfs.AddLink(pdf, annotation);
        return AccessibilityPdfs.DefaultLayout(pdf, document) + AccessibilityPdfs.Kids(AccessibilityPdfs.AddObjectElement(pdf, "Link", document, link, element));
    }

    /// <summary>Adds the default elements, a widget and an element of a type that refers to it.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element.</param>
    /// <param name="widget">The widget's entries.</param>
    /// <param name="type">The element's type.</param>
    /// <returns>The kids.</returns>
    private static string AddWidget(TaggedPdfBuilder pdf, int document, string widget, string type) => AddObject(pdf, document, widget, type);

    /// <summary>Adds the default elements, an annotation and an element of a type that refers to it.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element.</param>
    /// <param name="entries">The annotation's entries.</param>
    /// <param name="type">The element's type.</param>
    /// <returns>The kids.</returns>
    private static string AddObject(TaggedPdfBuilder pdf, int document, string entries, string type)
    {
        var annotation = AccessibilityPdfs.AddAnnotation(pdf, entries);
        return AccessibilityPdfs.DefaultLayout(pdf, document) + AccessibilityPdfs.Kids(AccessibilityPdfs.AddObjectElement(pdf, type, document, annotation, string.Empty));
    }
}
