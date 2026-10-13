// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Keeps the document-level structures of copied pages: form fields, outlines, named destinations, optional content
/// groups and the names of embedded files, and retargets the destinations of copied annotations. A copy runs in this
/// order: <see cref="AddPage"/> for every page, <see cref="PrepareAnnotations"/> for every page, the copy itself, then
/// <see cref="Finish"/>. Annotations whose destination leads to a page that is not copied lose it, so no link points at
/// nothing.
/// </summary>
/// <remarks>Not thread-safe. The source must stay open until the target is saved.</remarks>
[DebuggerDisplay("PdfDocumentCarrier")]
internal sealed class PdfDocumentCarrier
{
    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>The destination retargeting.</summary>
    private readonly PdfCarryDestinations _destinations;

    /// <summary>The form fields.</summary>
    private readonly PdfCarryForms _forms;

    /// <summary>The source page objects whose annotations are prepared.</summary>
    private readonly HashSet<int> _prepared = [];

    /// <summary>Initializes a new instance of the <see cref="PdfDocumentCarrier"/> class.</summary>
    /// <param name="sink">The page copier.</param>
    internal PdfDocumentCarrier(IPdfCarrySink sink)
    {
        _context = new(sink);
        _destinations = new(_context);
        _forms = new(_context);
    }

    /// <summary>Notes that a source page is copied, so links to it are kept and now name the copy.</summary>
    /// <param name="source">The source page's object.</param>
    /// <param name="target">The copy's object.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddPage(PdfObjectId source, PdfObjectId target) => _context.AddPage(source, target);

    /// <summary>
    /// Lists the annotations of a copied page for copying: widgets keep their field, and destinations that lead to pages
    /// that are not copied are removed. The first copy of a page shares annotation objects with the source's field tree;
    /// later copies of the same page get direct annotations, which keep their look but join no field. A trap network that
    /// no longer matches the page is left out (see <see cref="PdfTrapNetworkCopy"/>).
    /// </summary>
    /// <param name="page">The source page.</param>
    /// <param name="filter">The annotations kept.</param>
    /// <param name="report">Receives what was done with the page's trap network, or <see langword="null"/>.</param>
    /// <returns>The values to copy as the page's <c>/Annots</c>, or <see langword="null"/> when there are none.</returns>
    internal PdfArray? PrepareAnnotations(PdfPage page, PdfAnnotationFilter filter, PdfPageEditReport? report)
    {
        var source = filter == PdfAnnotationFilter.None ? null : page.Dictionary.GetArray(KnownName.Annots);
        if (source is null)
        {
            return null;
        }

        var first = !page.Id.IsValid || _prepared.Add(page.Id.Number);
        var result = new PdfArray(null, source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            if (source.GetDictionary(i) is { } annotation && PdfPageImporter.Passes(annotation, filter))
            {
                result.Add(PrepareAnnotation(source.GetRaw(i), annotation, first));
            }
        }

        return PdfTrapNetworkCopy.Prepare(_context.Sink, page, source, result, report);
    }

    /// <summary>Sets the target's page labels.</summary>
    /// <param name="labels">The <c>/PageLabels</c> number tree.</param>
    internal void SetPageLabels(PdfDictionary labels)
    {
        _context.Catalog.Set(KnownName.PageLabels, PdfValue.FromDictionary(labels));
        _context.CatalogChanged = true;
    }

    /// <summary>Copies the structures the pages need into the target.</summary>
    /// <param name="report">Receives what the copy changed in the target's form, or <see langword="null"/>.</param>
    internal void Finish(PdfPageEditReport? report)
    {
        new PdfCarryOutlines(_context, _destinations).Finish();
        _destinations.Finish();
        new PdfCarryFiles(_context).Finish();
        new PdfCarryLayers(_context).Finish();
        _forms.Finish(report);
        _context.Flush();
    }

    /// <summary>Prepares one annotation.</summary>
    /// <param name="raw">The annotation as listed in the page's <c>/Annots</c>.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="first">Whether this is the first copy of its page.</param>
    /// <returns>What to copy in its place: the reference, or a direct copy.</returns>
    private PdfValue PrepareAnnotation(PdfValue raw, PdfDictionary annotation, bool first)
    {
        var number = raw.AsReference().Number;
        var copy = annotation.Clone();
        var touched = _destinations.Fix(copy);
        if (number == 0 || !first)
        {
            return !touched && first ? raw : PdfValue.FromDictionary(copy);
        }

        if (annotation.IsName(KnownName.Subtype, KnownName.Widget))
        {
            var widget = _forms.Prepare(copy, number);
            _context.Sink.Override(number, widget.Dictionary, widget.KeepParent);
        }
        else if (touched)
        {
            _context.Sink.Override(number, copy, false);
        }

        return raw;
    }
}
