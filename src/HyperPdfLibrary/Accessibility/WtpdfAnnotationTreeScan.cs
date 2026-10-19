// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks the annotations the structure tree holds (WTPDF 1.0, clauses 8.9.2.3, 8.9.2.4.9, 8.9.2.4.16, 8.9.4.2 and 8.10.2.3): a popup is
/// not in the tree, markup and watermark annotations sit in an <c>Annot</c> tag, an annotation's <c>Contents</c> matches the <c>Alt</c> of
/// its tag, and a form field widget has a label or a <c>Contents</c>.
/// </summary>
internal static class WtpdfAnnotationTreeScan
{
    /// <summary>Walks the tree and checks each annotation it holds.</summary>
    /// <param name="store">The document's object store.</param>
    /// <param name="tree">The structure tree, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfObjectStore store, PdfStructureTree? tree, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        if (tree is null)
        {
            return;
        }

        StructureWalk.Each(tree, new TreeState(store, findings), Visit, cancellationToken);
    }

    /// <summary>Determines whether a subtype is a markup annotation, one whose meaning the annotation type gives it.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns><see langword="true"/> for a markup annotation.</returns>
    internal static bool IsMarkup(KnownName subtype) => IsTextMarkup(subtype) || IsDrawnMarkup(subtype);

    /// <summary>Determines whether a subtype is a markup annotation that marks up text or notes.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns><see langword="true"/> for one.</returns>
    private static bool IsTextMarkup(KnownName subtype) =>
        subtype is KnownName.Text or KnownName.FreeText or KnownName.Highlight or KnownName.Underline or KnownName.Squiggly or KnownName.StrikeOut or KnownName.Caret;

    /// <summary>Determines whether a subtype is a markup annotation that draws or attaches something.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns><see langword="true"/> for one.</returns>
    private static bool IsDrawnMarkup(KnownName subtype) =>
        subtype is KnownName.Line or KnownName.Square or KnownName.Circle or KnownName.Polygon or KnownName.PolyLine
            or KnownName.Stamp or KnownName.Ink or KnownName.FileAttachment or KnownName.Redact;

    /// <summary>Checks the annotations one element references.</summary>
    /// <param name="element">The element.</param>
    /// <param name="state">The store and the findings.</param>
    private static void Visit(PdfStructureElement element, TreeState state)
    {
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is not null || kid.Kind != PdfStructureKidKind.Object || !kid.Object.IsValid)
            {
                continue;
            }

            if (StoreReading.Resolve(state.Store, PdfValue.FromReference(kid.Object)).AsDictionary() is { } annotation && !annotation.Get(KnownName.Subtype).IsNull)
            {
                CheckAnnotation(element, annotation, state.Findings);
            }
        }
    }

    /// <summary>Checks one annotation against the element that holds it.</summary>
    /// <param name="element">The element.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckAnnotation(PdfStructureElement element, PdfDictionary annotation, List<PdfWtpdfFinding> findings)
    {
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        if (subtype == KnownName.Popup)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4.9", "A popup annotation cannot be in the structure tree.", element.Id.Number));
        }

        if (InArtifact(element))
        {
            return;
        }

        var contents = annotation.GetText(KnownName.Contents);
        CheckEnclosure(element, subtype, findings);
        if (contents is not null && element.AlternateText is not null && !string.Equals(contents, element.AlternateText, StringComparison.Ordinal))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.4.2", "An annotation's Contents must be the same as the Alt of the tag that holds it.", element.Id.Number));
        }

        if (subtype == KnownName.Widget)
        {
            CheckWidget(element, annotation, contents, findings);
        }
    }

    /// <summary>Requires a markup or watermark annotation to be in an <c>Annot</c> tag.</summary>
    /// <param name="element">The element that holds the annotation.</param>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckEnclosure(PdfStructureElement element, KnownName subtype, List<PdfWtpdfFinding> findings)
    {
        if (element.MappedType == "Annot")
        {
            return;
        }

        if (IsMarkup(subtype))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.3", "A markup annotation used as content must be enclosed in an Annot tag.", element.Id.Number));
        }
        else if (subtype == KnownName.Watermark)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4.16", "A watermark annotation used as content must be enclosed in an Annot tag.", element.Id.Number));
        }
    }

    /// <summary>Requires a form field's widget to have a label in the tree or a <c>Contents</c>, and a <c>Contents</c> when it has additional actions.</summary>
    /// <param name="element">The element that holds the widget.</param>
    /// <param name="widget">The widget annotation.</param>
    /// <param name="contents">The widget's Contents, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckWidget(PdfStructureElement element, PdfDictionary widget, string? contents, List<PdfWtpdfFinding> findings)
    {
        var isField = !widget.Get(KnownName.FT).IsNull || !widget.Get(KnownName.Parent).IsNull;
        if (!isField || contents is not null)
        {
            return;
        }

        if (!widget.Get(KnownName.AA).IsNull)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.2.3", "A widget with additional actions needs a Contents entry.", element.Id.Number));
        }
        else if (!HasLabel(element))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.2.3", "A widget needs a label (an Lbl tag beside it) or a Contents entry.", element.Id.Number));
        }
    }

    /// <summary>Determines whether an Lbl tag sits in an element or its parent.</summary>
    /// <param name="element">The element that holds the widget.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasLabel(PdfStructureElement element) => HoldsLabel(element) || (element.Parent is { } parent && HoldsLabel(parent));

    /// <summary>Determines whether an element has an Lbl tag as a child.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HoldsLabel(PdfStructureElement element)
    {
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is { MappedType: "Lbl" })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an element is, or is inside, an Artifact tag.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool InArtifact(PdfStructureElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.MappedType == "Artifact")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What the walk passes to each element.</summary>
    /// <param name="Store">The document's object store, which resolves the annotations.</param>
    /// <param name="Findings">Receives the findings.</param>
    private readonly record struct TreeState(PdfObjectStore Store, List<PdfWtpdfFinding> Findings);
}
