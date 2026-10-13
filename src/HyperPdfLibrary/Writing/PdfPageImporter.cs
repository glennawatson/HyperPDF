// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Copies pages of one document into a <see cref="PdfDocumentBuilder"/>, either as pages of their own or as form
/// XObjects that a sheet can draw, with printable annotations flattened into the form.
/// </summary>
/// <remarks>Not thread-safe. The source document must stay open until the new document is saved.</remarks>
[DebuggerDisplay("PdfPageImporter: {Objects}")]
public sealed class PdfPageImporter
{
    /// <summary>A quarter turn in degrees.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn in degrees.</summary>
    private const int HalfTurn = 180;

    /// <summary>Three quarter turns in degrees.</summary>
    private const int ThreeQuarterTurn = 270;

    /// <summary>The document being built.</summary>
    private readonly PdfDocumentBuilder _target;

    /// <summary>The document copied from.</summary>
    private readonly PdfDocument _source;

    /// <summary>The token for this page copy.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>Copies the objects pages refer to.</summary>
    private readonly PdfObjectImporter _objects;

    /// <summary>Keeps the document-level structures of the pages; <see langword="null"/> when only the pages are copied.</summary>
    private readonly PdfDocumentCarrier? _carrier;

    /// <summary>Initializes a new instance of the <see cref="PdfPageImporter"/> class.</summary>
    /// <param name="target">The document being built.</param>
    /// <param name="source">The document copied from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PdfPageImporter(PdfDocumentBuilder target, PdfDocument source)
        : this(target, source, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfPageImporter"/> class.</summary>
    /// <param name="target">The document being built.</param>
    /// <param name="source">The document copied from.</param>
    /// <param name="carryStructures">Whether <see cref="ImportPages"/> also keeps forms, outlines, destinations, labels, layers and embedded file names.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal PdfPageImporter(PdfDocumentBuilder target, PdfDocument source, bool carryStructures, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        _target = target;
        _source = source;
        _cancellationToken = cancellationToken;
        _objects = new(target, source.Objects, cancellationToken);
        _carrier = carryStructures ? new(_objects) : null;
    }

    /// <summary>Gets the importer that copies the objects pages refer to.</summary>
    public PdfObjectImporter Objects => _objects;

    /// <summary>Gets the matrix that turns a box of a rotated page upright with its lower-left corner at the origin.</summary>
    /// <param name="box">The page box, in user space.</param>
    /// <param name="rotation">The page's clockwise rotation: 0, 90, 180 or 270.</param>
    /// <returns>The matrix.</returns>
    public static Matrix3x2 GetPageMatrix(in PdfRectangle box, int rotation) => rotation switch
    {
        QuarterTurn => new(0, -1, 1, 0, -box.Bottom, box.Right),
        HalfTurn => new(-1, 0, 0, -1, box.Right, box.Top),
        ThreeQuarterTurn => new(0, 1, -1, 0, box.Top, -box.Left),
        _ => new(1, 0, 0, 1, -box.Left, -box.Bottom),
    };

    /// <summary>Gets the size of a box as displayed after a rotation.</summary>
    /// <param name="box">The page box.</param>
    /// <param name="rotation">The page's clockwise rotation: 0, 90, 180 or 270.</param>
    /// <param name="width">The displayed width.</param>
    /// <param name="height">The displayed height.</param>
    public static void GetDisplayedSize(in PdfRectangle box, int rotation, out float width, out float height)
    {
        var sideways = rotation is QuarterTurn or ThreeQuarterTurn;
        width = sideways ? box.Height : box.Width;
        height = sideways ? box.Width : box.Height;
    }

    /// <summary>
    /// Copies a page as a page of the new document, keeping its content, resources, boxes, rotation and chosen annotations.
    /// Its separation dictionary is left out, because it names other pages that this copy does not carry.
    /// </summary>
    /// <param name="page">The source page.</param>
    /// <param name="annotations">The annotations kept.</param>
    /// <returns>The new page's id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The source cannot be read.</exception>
    public PdfObjectId ImportPage(PdfPage page, PdfAnnotationFilter annotations)
    {
        ArgumentNullException.ThrowIfNull(page);
        var id = _target.Reserve();
        if (page.Id.IsValid)
        {
            // Annotations that name their page, and links to it, now name the copy.
            _objects.MapObject(page.Id, id);
        }

        return ImportPageCore(page, id, null, annotations, default);
    }

    /// <summary>
    /// Copies a page as a form XObject: its content, then its printable annotations, drawn in the page's user space.
    /// The form's box is <paramref name="box"/>, which clips the content.
    /// </summary>
    /// <param name="page">The source page.</param>
    /// <param name="box">The part of the page the form shows, in user space.</param>
    /// <param name="annotations">The annotations drawn into the form.</param>
    /// <param name="applyRotation">Whether the form's matrix turns the page upright; otherwise the form keeps user space.</param>
    /// <returns>The form's id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The source cannot be read.</exception>
    public PdfObjectId ImportForm(PdfPage page, in PdfRectangle box, PdfAnnotationFilter annotations, bool applyRotation)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PdfFormImporter(_objects).ImportForm(page, box, annotations, applyRotation, default);
    }

    /// <summary>Determines whether an annotation passes a filter.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="filter">The filter.</param>
    /// <returns><see langword="true"/> when the annotation is kept.</returns>
    internal static bool Passes(PdfDictionary annotation, PdfAnnotationFilter filter) => filter switch
    {
        PdfAnnotationFilter.All => true,
        PdfAnnotationFilter.WidgetsOnly => annotation.IsName(KnownName.Subtype, KnownName.Widget),
        _ => false,
    };

    /// <summary>
    /// Copies pages as pages of the new document and keeps what they need from the rest of their document: the form fields
    /// behind their widgets, the outline entries and named destinations that lead to them, their page labels, their
    /// optional content groups and the names of their embedded files. Links to pages that are not copied are removed.
    /// </summary>
    /// <param name="pages">The source pages, in order.</param>
    /// <param name="report">Receives the printed pages of a preseparated document that were not copied whole, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The importer was not created to keep structures.</exception>
    internal void ImportPages(ReadOnlySpan<PdfPage> pages, PdfPageEditReport? report)
    {
        var carrier = _carrier ?? throw new InvalidOperationException("The importer does not keep document structures.");
        var separations = PdfCarrySeparations.Plan(_source, pages, report);
        var ids = new PdfObjectId[pages.Length];
        var prepared = new PdfArray?[pages.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            ids[i] = _target.Reserve();
            carrier.AddPage(pages[i].Id, ids[i]);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            prepared[i] = carrier.PrepareAnnotations(pages[i], PdfAnnotationFilter.All, report);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            _ = ImportPageCore(pages[i], ids[i], prepared[i], PdfAnnotationFilter.All, PdfCarrySeparations.Build(separations, i, _objects, ids));
        }

        if (PdfDocumentLabels.CollectSourceLabels(_source, pages) is { } labels)
        {
            carrier.SetPageLabels(PdfPageLabelWriter.CreateTree(null, PdfPageLabelWriter.Retarget(labels, _objects)));
        }

        carrier.Finish(report);
    }

    /// <summary>Lists the entries of an annotation array a filter keeps, as written.</summary>
    /// <param name="source">The array.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The entries.</returns>
    private static PdfArray Kept(PdfArray source, PdfAnnotationFilter filter)
    {
        var kept = new PdfArray(null, source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            if (source.GetDictionary(i) is { } annotation && Passes(annotation, filter))
            {
                kept.Add(source.GetRaw(i));
            }
        }

        return kept;
    }

    /// <summary>Copies a page into the object reserved for it.</summary>
    /// <param name="page">The source page.</param>
    /// <param name="id">The reserved object.</param>
    /// <param name="prepared">The annotations prepared by the carrier, or <see langword="null"/> to copy the filtered ones.</param>
    /// <param name="annotations">The annotations kept when none are prepared.</param>
    /// <param name="separation">The page's new separation dictionary, or null for none.</param>
    /// <returns>The new page's id.</returns>
    private PdfObjectId ImportPageCore(PdfPage page, PdfObjectId id, PdfArray? prepared, PdfAnnotationFilter annotations, PdfValue separation)
    {
        var copy = new PdfDictionary(null);
        var source = page.Dictionary;

        // The source separation dictionary names pages of the source; only a dictionary planned for the copy is written.
        var separationKey = SeparationReader.InfoKey(_source.Objects.Names);
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (!key.Is(KnownName.Type) && !key.Is(KnownName.Parent) && !key.Is(KnownName.Annots) && key != separationKey)
            {
                copy.Add(CopyKey(key), _objects.Import(source.GetValueAt(i)));
            }
        }

        if (!separation.IsNull)
        {
            copy.Set(SeparationReader.InfoKey(_target.Names), separation);
        }

        SetInheritedAttributes(copy, page);
        var annots = prepared is null ? ImportAnnotations(page, annotations) : ImportPrepared(prepared);
        if (annots.Count > 0)
        {
            copy.Set(KnownName.Annots, PdfValue.FromArray(annots));
        }

        return _target.AddPage(id, copy);
    }

    /// <summary>Moves a dictionary key to the new document's name table.</summary>
    /// <param name="key">The source key.</param>
    /// <returns>The key in the new document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfName CopyKey(PdfName key) => _objects.ImportName(key);

    /// <summary>Sets the box, rotation and resources a page inherits from its page tree.</summary>
    /// <param name="copy">The page copy.</param>
    /// <param name="page">The source page.</param>
    private void SetInheritedAttributes(PdfDictionary copy, PdfPage page)
    {
        copy.Set(KnownName.MediaBox, PdfValue.FromArray(page.MediaBox.ToArray(null)));
        if (page.CropBox != page.MediaBox)
        {
            copy.Set(KnownName.CropBox, PdfValue.FromArray(page.CropBox.ToArray(null)));
        }

        if (page.Rotation != 0)
        {
            copy.Set(KnownName.Rotate, PdfValue.FromInteger(page.Rotation));
        }

        if (!copy.ContainsKey(KnownName.Resources))
        {
            copy.Set(KnownName.Resources, page.Resources is { } resources ? _objects.Import(PdfValue.FromDictionary(resources)) : PdfValue.FromDictionary(new(null)));
        }
    }

    /// <summary>
    /// Copies the annotations a filter keeps; each becomes an object of its own. On a page with a trap network, the
    /// network is checked first and left out when it no longer matches the page (see <see cref="PdfTrapNetworkCopy"/>).
    /// </summary>
    /// <param name="page">The source page.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The references to the copies.</returns>
    private PdfArray ImportAnnotations(PdfPage page, PdfAnnotationFilter filter)
    {
        var annots = new PdfArray(null);
        var source = filter == PdfAnnotationFilter.None ? null : page.Dictionary.GetArray(KnownName.Annots);
        if (source is not null && PdfTrapNetworkReader.Find(_source.Objects, source, out _) > 0)
        {
            return ImportPrepared(PdfTrapNetworkCopy.Prepare(_objects, page, source, Kept(source, filter), null));
        }

        for (var i = 0; source is not null && i < source.Count; i++)
        {
            if (source.GetDictionary(i) is { } annotation && Passes(annotation, filter))
            {
                annots.Add(PdfValue.FromReference(_target.Add(_objects.Import(PdfValue.FromDictionary(annotation)))));
            }
        }

        return annots;
    }

    /// <summary>Copies annotations the carrier prepared; one it shares with a field keeps its place in the form.</summary>
    /// <param name="prepared">The values to copy: references, or direct annotations.</param>
    /// <returns>The references to the copies.</returns>
    private PdfArray ImportPrepared(PdfArray prepared)
    {
        var annots = new PdfArray(null, prepared.Count);
        foreach (var item in prepared.Items)
        {
            var copy = _objects.Import(item);
            if (!copy.IsNull)
            {
                annots.Add(copy.IsReference ? copy : PdfValue.FromReference(_target.Add(copy)));
            }
        }

        return annots;
    }
}
