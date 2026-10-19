// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks page content, annotations, tab order and table cell labels.</summary>
internal static class AccessibilityPageScan
{
    /// <summary>The annotation flag bit for Hidden.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The annotation flag bit for NoView.</summary>
    private const int NoViewFlag = 1 << 5;

    /// <summary>The most <c>/Parent</c> steps followed to find a field's label.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>Checks a page.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>What was counted on the page.</returns>
    internal static PdfAccessibilityPage Check(ref AccessibilityPageState state, int pageIndex)
    {
        var page = PdfDocumentPages.GetPage(state.Document, pageIndex);
        var content = PdfDocumentTagged.GetMarkedContent(state.Document, pageIndex);
        CountGlyphs(content, out var untagged, out var artifacts);
        var untaggedGraphics = content.UntaggedGraphicCount;
        if (state.Tree is not null && (untagged > 0 || untaggedGraphics > 0))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.UntaggedContent, pageIndex);
        }

        if (content.InvalidLanguageCount > 0)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.LanguageInvalid, pageIndex);
        }

        var reachable = CheckAnnotations(ref state, page, pageIndex);
        CheckReferenceXObjects(ref state, page, pageIndex);
        var tabs = page.Dictionary.IsName(KnownName.Tabs, KnownName.S);
        if (NeedsStructureTabs(page.Dictionary, tabs, state.Part, reachable))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.TabsNotStructure, pageIndex);
        }

        if (state.TablePages is { } tables && (tables.Contains(pageIndex) || tables.Contains(-1)))
        {
            CheckTables(ref state, pageIndex);
        }

        return new(pageIndex, reachable, tabs, untagged, artifacts, untaggedGraphics);
    }

    /// <summary>Determines which accessibility checks apply without decoding an annotation's subtype.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The annotation kind.</returns>
    internal static AnnotationKind Classify(PdfDictionary annotation) =>
        (annotation.GetInt32(KnownName.F, 0) & (HiddenFlag | NoViewFlag)) != 0
            ? AnnotationKind.Skipped
            : annotation.GetName(KnownName.Subtype).ToKnownName() switch
        {
            KnownName.Popup or KnownName.PrinterMark or KnownName.TrapNet => AnnotationKind.Skipped,
            KnownName.Link => AnnotationKind.Link,
            KnownName.Widget => AnnotationKind.Widget,
            _ => AnnotationKind.Other,
        };

    /// <summary>Determines whether a page with reachable annotations has a tab order the claim does not allow.</summary>
    /// <param name="page">The page's dictionary, whose <c>/Tabs</c> value is read.</param>
    /// <param name="followsStructure">Whether <c>/Tabs</c> is <c>/S</c>.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="reachable">The number of annotations the keyboard reaches.</param>
    /// <returns><see langword="true"/> when it should be reported.</returns>
    private static bool NeedsStructureTabs(PdfDictionary page, bool followsStructure, int? part, int reachable) =>
        reachable > 0 && !followsStructure
        && !(part == PdfUaClaim.Part2 && (page.IsName(KnownName.Tabs, KnownName.W) || page.IsName(KnownName.Tabs, KnownName.A)));

    /// <summary>Counts the visible glyphs drawn outside marked content and outside artifacts, and those drawn as artifacts.</summary>
    /// <param name="content">What the page draws.</param>
    /// <param name="untagged">Receives the glyphs drawn outside marked content and artifacts.</param>
    /// <param name="artifacts">Receives the glyphs drawn as artifacts.</param>
    private static void CountGlyphs(PdfMarkedContentPage content, out int untagged, out int artifacts)
    {
        untagged = 0;
        artifacts = 0;
        var glyphs = content.Glyphs;
        for (var i = 0; i < glyphs.Length; i++)
        {
            ref readonly var glyph = ref glyphs[i];
            if (glyph.IsArtifact)
            {
                artifacts += IsVisible(content.GetText(i)) ? 1 : 0;
            }
            else if (glyph.Mcid < 0)
            {
                untagged += IsVisible(content.GetText(i)) ? 1 : 0;
            }
        }
    }

    /// <summary>Determines whether a glyph's text shows: a glyph with no text counts, white space does not.</summary>
    /// <param name="text">The glyph's text.</param>
    /// <returns><see langword="true"/> when it shows.</returns>
    private static bool IsVisible(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return true;
        }

        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a widget's label: the <c>/TU</c> of its field, which is the widget itself or the nearest parent with a partial
    /// name <c>/T</c>, or the <c>/Alt</c> of the structure element that holds the widget (ISO 14289-1 7.18.1).
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <param name="owner">The structure element that refers to the widget, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the widget has a label.</returns>
    private static bool HasFieldLabel(PdfDictionary widget, PdfStructureElement? owner)
    {
        if (owner?.AlternateText is not null)
        {
            return true;
        }

        var field = widget;
        for (var depth = 0; depth < MaxFieldDepth && !field.ContainsKey(KnownName.T) && field.GetDictionary(KnownName.Parent) is { } parent; depth++)
        {
            field = parent;
        }

        return field.GetText(KnownName.TU) is { Length: > 0 };
    }

    /// <summary>Counts a table's cells that no header cell labels, without entering nested tables.</summary>
    /// <param name="node">The table or one of its descendants.</param>
    /// <param name="isRoot">Whether the node is the table itself.</param>
    /// <param name="hasHeader">Set when the table has a header cell.</param>
    /// <returns>The number of unlabelled data cells.</returns>
    private static int CountUnlabelledCells(PdfSemanticNode node, bool isRoot, ref bool hasHeader)
    {
        if (!isRoot && node.Role == PdfSemanticRole.Table)
        {
            return 0;
        }

        hasHeader |= node.Role == PdfSemanticRole.TableHeaderCell;
        var count = node.Role == PdfSemanticRole.TableCell && node.Headers.Count == 0 ? 1 : 0;
        foreach (var child in node.Children)
        {
            count += CountUnlabelledCells(child, false, ref hasHeader);
        }

        return count;
    }

    /// <summary>Checks every annotation on a page.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="page">The page.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The number of annotations a reader can reach.</returns>
    private static int CheckAnnotations(ref AccessibilityPageState state, PdfPage page, int pageIndex)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        var reachable = 0;
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            state.Cancellation.ThrowIfCancellationRequested();
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            AccessibilityEmbeddedFileScan.CheckAnnotation(state.Document.Objects.Names, annotation, state.Part, state.Findings, pageIndex);
            CheckTrapNet(ref state, annotation, pageIndex);
            AccessibilityMediaScan.Check(state.Document.Objects.Names, annotation, state.Part, state.Findings, pageIndex);
            var kind = Classify(annotation);
            if (kind == AnnotationKind.Skipped)
            {
                continue;
            }

            reachable++;
            CheckAnnotation(ref state, annotation, annotations.GetRaw(i).AsReference(), kind, pageIndex);
        }

        return reachable;
    }

    /// <summary>Reports a form XObject in the page's resources that has a <c>/Ref</c> entry, which ISO 14289-1 clause 7.20 does not permit.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="page">The page.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckReferenceXObjects(ref AccessibilityPageState state, PdfPage page, int pageIndex)
    {
        if (state.Part != PdfUaClaim.Part1 || page.Resources?.GetDictionary(KnownName.XObject) is not { } xobjects)
        {
            return;
        }

        for (var i = 0; i < xobjects.Count; i++)
        {
            state.Cancellation.ThrowIfCancellationRequested();
            if (xobjects.Get(xobjects.GetKeyAt(i)).AsStream() is { } stream && stream.Dictionary.ContainsKey(KnownName.Ref))
            {
                AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.ReferenceXObject, pageIndex);
            }
        }
    }

    /// <summary>Reports a TrapNet annotation, which ISO 14289-1 clause 7.18.2 does not permit, whether or not it is hidden.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckTrapNet(ref AccessibilityPageState state, PdfDictionary annotation, int pageIndex)
    {
        if (state.Part == PdfUaClaim.Part1 && annotation.GetName(KnownName.Subtype).ToKnownName() == KnownName.TrapNet)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.TrapNetAnnotation, pageIndex);
        }
    }

    /// <summary>Checks one annotation.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">Its object id.</param>
    /// <param name="kind">Which checks apply.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckAnnotation(ref AccessibilityPageState state, PdfDictionary annotation, PdfObjectId id, AnnotationKind kind, int pageIndex)
    {
        var owner = FindOwner(ref state, annotation, id);
        switch (kind)
        {
            case AnnotationKind.Link:
                {
                    CheckLink(ref state, annotation, owner, pageIndex);
                    break;
                }

            case AnnotationKind.Widget:
                {
                    CheckWidget(ref state, annotation, owner, pageIndex);
                    break;
                }

            default:
                {
                    CheckOther(ref state, owner, pageIndex);
                    break;
                }
        }
    }

    /// <summary>Finds the structure element that refers to an annotation.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">Its object id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    private static PdfStructureElement? FindOwner(ref AccessibilityPageState state, PdfDictionary annotation, PdfObjectId id) =>
        id.IsValid && state.Owners is { } owners && owners.TryGetValue(id.Number, out var owner) ? owner : state.Tree?.GetObjectParent(annotation);

    /// <summary>Checks that a link is tagged as a link and has a description.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="link">The link annotation.</param>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckLink(ref AccessibilityPageState state, PdfDictionary link, PdfStructureElement? owner, int pageIndex)
    {
        var id = owner?.Id ?? default;
        if (state.Tree is not null && owner?.Type != PdfStructureType.Link)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.LinkNotTagged, pageIndex, id);
        }

        if (link.GetText(KnownName.Contents) is not { Length: > 0 } && owner?.AlternateText is null)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.LinkNoDescription, pageIndex, id);
        }

        CheckImageMap(ref state, link, pageIndex, id);
    }

    /// <summary>Reports a link whose URI action is a server-side image map, which ISO 14289-1 clause 7.18.5 does not permit.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="link">The link annotation.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="id">The element that refers to the link, or the default id.</param>
    private static void CheckImageMap(ref AccessibilityPageState state, PdfDictionary link, int pageIndex, PdfObjectId id)
    {
        if (state.Part == PdfUaClaim.Part1 && UsesImageMap(state.Document.Objects.Names, link))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.LinkIsMap, pageIndex, id);
        }
    }

    /// <summary>Determines whether a link's URI action has <c>/IsMap true</c> (ISO 32000-1 12.6.4.7).</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="link">The link annotation.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool UsesImageMap(PdfNameTable names, PdfDictionary link) =>
        link.GetDictionary(KnownName.A) is { } action
        && action.IsName(KnownName.S, KnownName.URI)
        && action.GetBoolean(names.Intern("IsMap"u8), false);

    /// <summary>Checks that a form field has a label and is tagged as a form field.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="widget">The widget annotation.</param>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckWidget(ref AccessibilityPageState state, PdfDictionary widget, PdfStructureElement? owner, int pageIndex)
    {
        var id = owner?.Id ?? default;
        if (!HasFieldLabel(widget, owner))
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.FormFieldNoLabel, pageIndex, id);
        }

        if (state.Tree is not null && owner?.Type != PdfStructureType.Form)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.FormFieldNotTagged, pageIndex, id);
        }
    }

    /// <summary>Checks that any other annotation is referred to from the tree, as an Annot for PDF/UA-1.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckOther(ref AccessibilityPageState state, PdfStructureElement? owner, int pageIndex)
    {
        if (state.Tree is null)
        {
            return;
        }

        if (owner is null)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.AnnotationNotTagged, pageIndex);
        }
        else if (state.Part != PdfUaClaim.Part2 && owner.Type != PdfStructureType.Annotation)
        {
            AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.AnnotationWrongStructureType, pageIndex, owner.Id);
        }
    }

    /// <summary>Checks the tables on a page as the reader sees them, once the header cells are linked to the cells they label.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckTables(ref AccessibilityPageState state, int pageIndex)
    {
        foreach (var node in PdfReadingStructure.ReadTagged(state.Document, pageIndex).Nodes)
        {
            CheckTableNodes(ref state, node, pageIndex);
        }
    }

    /// <summary>Walks a node and its descendants for tables.</summary>
    /// <param name="state">The operation's exclusively owned state.</param>
    /// <param name="node">The node.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void CheckTableNodes(ref AccessibilityPageState state, PdfSemanticNode node, int pageIndex)
    {
        if (node.Role == PdfSemanticRole.Table && node.Element is { } element)
        {
            if (state.CheckedTables is null)
            {
                state = state with
                {
                    CheckedTables = state.Document.Objects.ScratchPools.Slots<HashSet<PdfStructureElement>>().Take() ?? [with(ReferenceEqualityComparer.Instance)]
                };
            }

            if (state.CheckedTables.Add(element))
            {
                var hasHeader = false;
                if (CountUnlabelledCells(node, true, ref hasHeader) > 0 && hasHeader)
                {
                    AccessibilityFindingCollection.Add(state.Findings, PdfAccessibilityCode.TableCellNoHeader, pageIndex, element.Id);
                }
            }
        }

        foreach (var child in node.Children)
        {
            CheckTableNodes(ref state, child, pageIndex);
        }
    }
}
