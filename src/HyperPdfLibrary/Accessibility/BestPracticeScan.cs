// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Suggests improvements from the Tagged PDF Best Practice Guide: Syntax, version 1.0.1. Nothing here is a conformance rule.</summary>
internal static class BestPracticeScan
{
    /// <summary>Checks a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The advice, in document order; empty when the file is untagged.</returns>
    internal static PdfTagAdvice[] Check(PdfDocument document, CancellationToken cancellationToken)
    {
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return [];
        }

        var advice = new List<PdfTagAdvice>();
        var scan = new AdviceScan(advice);
        StructureWalk.Each(tree, scan, Visit, cancellationToken);
        if (scan.HasHeading && document.Catalog.GetDictionary(KnownName.Outlines) is null)
        {
            advice.Add(new("7.2", "A document with headings is easier to navigate with outlines (bookmarks) that follow the headings.", 0));
        }

        return [.. advice];
    }

    /// <summary>Checks one tag during the walk.</summary>
    /// <param name="element">The tag.</param>
    /// <param name="scan">The walk's state.</param>
    private static void Visit(PdfStructureElement element, AdviceScan scan)
    {
        scan.HasHeading |= element.HeadingLevel > 0;
        CheckElement(element, scan.Advice);
    }

    /// <summary>Checks one tag.</summary>
    /// <param name="element">The tag.</param>
    /// <param name="advice">Receives the advice.</param>
    private static void CheckElement(PdfStructureElement element, List<PdfTagAdvice> advice)
    {
        CheckEmpty(element, advice);
        var parentType = element.Parent?.MappedType;
        if (element.MappedType == "P" && parentType == "P")
        {
            advice.Add(new("4.2.1", "A paragraph tag holds another paragraph tag. Each paragraph should have its own tag, side by side.", element.Id.Number));
        }

        if (element.MappedType == "Caption" && parentType == "L")
        {
            CheckListCaption(element, element.Parent!, advice);
        }
    }

    /// <summary>Suggests that a tag that is not meant to be empty holds something (guide clause 3.5).</summary>
    /// <param name="element">The tag.</param>
    /// <param name="advice">Receives the advice.</param>
    private static void CheckEmpty(PdfStructureElement element, List<PdfTagAdvice> advice)
    {
        if (element.Kids.Count == 0 && element.MappedType is not ("TD" or "LI" or "Span" or "Div" or "Document" or "NonStruct" or "Private"))
        {
            advice.Add(new("3.5", $"The {element.RawType} tag is empty. Only tags that keep a table, list or grouping in shape should be.", element.Id.Number));
        }
    }

    /// <summary>Suggests that a list's caption comes before its items (guide clause 4.2.5).</summary>
    /// <param name="caption">The Caption.</param>
    /// <param name="list">The list holding it.</param>
    /// <param name="advice">Receives the advice.</param>
    private static void CheckListCaption(PdfStructureElement caption, PdfStructureElement list, List<PdfTagAdvice> advice)
    {
        foreach (var kid in list.KidSpan)
        {
            if (ReferenceEquals(kid.Element, caption))
            {
                return;
            }

            if (kid.Element?.MappedType != "LI")
            {
                continue;
            }

            advice.Add(new("4.2.5", "A list's caption should come before its items.", caption.Id.Number));
            return;
        }
    }

    /// <summary>The state of one walk: where the advice goes and whether a heading was seen.</summary>
    /// <param name="advice">Receives the advice.</param>
    private sealed class AdviceScan(List<PdfTagAdvice> advice)
    {
        /// <summary>Gets where the advice goes.</summary>
        internal List<PdfTagAdvice> Advice { get; } = advice;

        /// <summary>Gets or sets a value indicating whether any tag is a heading.</summary>
        internal bool HasHeading { get; set; }
    }
}
