// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Puts annotations that the structure tree does not mention into it as <c>Annot</c> elements (ISO 14289-1:2014, 7.18.1;
/// ISO 32000-1:2008, 14.8.4.4.3). An annotation needs a description, so only one with <c>/Contents</c> is tagged; the
/// repair never invents a description. Links and form fields are not tagged here, as their element types carry meaning
/// that only the person who made them can judge (7.18.4, 7.18.5).
/// </summary>
internal static class PdfUaAnnotationTagging
{
    /// <summary>The annotation flag bit for Hidden.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The annotation flag bit for NoView.</summary>
    private const int NoViewFlag = 1 << 5;

    /// <summary>The most structure elements searched for annotation references.</summary>
    private const int MaxElementsSearched = 1 << 20;

    /// <summary>Tags the annotations that need it.</summary>
    /// <param name="document">The document, read after any earlier repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree
            || FormStructureWriter.Create(document, transaction, KnownName.Annot) is not { } writer)
        {
            return;
        }

        var referenced = new HashSet<int>();
        var budget = MaxElementsSearched;
        var roots = tree.Roots;
        for (var i = 0; i < roots.Count; i++)
        {
            CollectReferences(roots[i], referenced, ref budget);
        }

        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TagPage(PdfDocumentPages.GetPage(document, i), tree, referenced, writer, transaction);
        }

        writer.Commit();
    }

    /// <summary>Tags the annotations of one page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="tree">The structure tree.</param>
    /// <param name="referenced">The numbers of the objects the tree already refers to.</param>
    /// <param name="writer">Writes the new elements.</param>
    /// <param name="transaction">The edit.</param>
    private static void TagPage(PdfPage page, PdfStructureTree tree, HashSet<int> referenced, FormStructureWriter writer, PdfEditTransaction transaction)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            var id = annotations.GetRaw(i).AsReference();

            // An annotation written directly in the page's array has no object of its own to carry a structure parent key.
            if (!id.IsValid || annotations.GetDictionary(i) is not { } annotation || !NeedsTag(annotation, id, tree, referenced))
            {
                continue;
            }

            var copy = annotation.Clone();
            copy.Set(KnownName.StructParent, PdfValue.FromInteger(writer.Add(id, page.Id)));
            transaction.Replace(id, PdfValue.FromDictionary(copy));
        }
    }

    /// <summary>Determines whether an annotation is one a reader reaches, is not in the tree and has a description.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">Its object.</param>
    /// <param name="tree">The structure tree.</param>
    /// <param name="referenced">The numbers of the objects the tree already refers to.</param>
    /// <returns><see langword="true"/> when the repair tags it.</returns>
    private static bool NeedsTag(PdfDictionary annotation, PdfObjectId id, PdfStructureTree tree, HashSet<int> referenced)
    {
        if ((annotation.GetInt32(KnownName.F, 0) & (HiddenFlag | NoViewFlag)) != 0
            || annotation.GetText(KnownName.Contents) is not { Length: > 0 }
            || referenced.Contains(id.Number)
            || tree.GetObjectParent(annotation) is not null)
        {
            return false;
        }

        return annotation.NameText("Subtype") is not ("Popup" or "PrinterMark" or "TrapNet" or "Link" or "Widget");
    }

    /// <summary>Collects the numbers of the objects that elements refer to.</summary>
    /// <param name="element">The element.</param>
    /// <param name="referenced">Receives the object numbers.</param>
    /// <param name="budget">How many more elements may be searched.</param>
    private static void CollectReferences(PdfStructureElement element, HashSet<int> referenced, ref int budget)
    {
        if (budget <= 0)
        {
            return;
        }

        budget--;

        // An indexed loop, as enumerating the interface list would box its enumerator.
        var kids = element.Kids;
        for (var i = 0; i < kids.Count; i++)
        {
            var kid = kids[i];
            if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                _ = referenced.Add(kid.Object.Number);
            }
            else if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                CollectReferences(child, referenced, ref budget);
            }
        }
    }
}
