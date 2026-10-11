// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Filters;
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
    /// <summary>The annotation flag that hides an annotation.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The annotation flag that prints an annotation.</summary>
    private const int PrintFlag = 1 << 2;

    /// <summary>The prefix of the names given to flattened annotation forms.</summary>
    private const string AnnotationFormPrefix = "PdfFlat";

    /// <summary>The number of values in a transformation matrix.</summary>
    private const int MatrixValues = 6;

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
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal PdfPageImporter(PdfDocumentBuilder target, PdfDocument source, bool carryStructures)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        _target = target;
        _source = source;
        _objects = new(target, source.Objects);
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

    /// <summary>Copies a page as a page of the new document, keeping its content, resources, boxes, rotation and chosen annotations.</summary>
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

        return ImportPageCore(page, id, null, annotations);
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
        var appearances = new List<FlattenedAnnotation>();
        CollectFlattened(page, annotations, appearances);
        var form = new PdfDictionary(null);
        form.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        form.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        form.Set(KnownName.BBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [box.Left, box.Bottom, box.Right, box.Top])));
        if (applyRotation)
        {
            var matrix = GetPageMatrix(box, page.Rotation);
            form.Set(KnownName.Matrix, PdfValue.FromArray(PdfArray.FromNumbers(null, [matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32])));
        }

        form.Set(KnownName.Resources, BuildResources(page, appearances));
        var content = default(PooledBuffer);
        try
        {
            AppendContents(page, appearances.Count > 0, ref content);
            AppendAnnotations(appearances, ref content);
            return _target.Add(PdfValue.FromStream(new(form, content.ToArray())));
        }
        finally
        {
            content.Dispose();
        }
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
    /// <exception cref="InvalidOperationException">The importer was not created to keep structures.</exception>
    internal void ImportPages(ReadOnlySpan<PdfPage> pages)
    {
        var carrier = _carrier ?? throw new InvalidOperationException("The importer does not keep document structures.");
        var ids = new PdfObjectId[pages.Length];
        var prepared = new PdfArray?[pages.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            ids[i] = _target.Reserve();
            carrier.AddPage(pages[i].Id, ids[i]);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            prepared[i] = carrier.PrepareAnnotations(pages[i], PdfAnnotationFilter.All);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            _ = ImportPageCore(pages[i], ids[i], prepared[i], PdfAnnotationFilter.All);
        }

        if (PdfDocumentLabels.CollectSourceLabels(_source, pages) is { } labels)
        {
            carrier.SetPageLabels(PdfPageLabelWriter.CreateTree(null, PdfPageLabelWriter.Retarget(labels, _objects)));
        }

        carrier.Finish();
    }

    /// <summary>Determines whether an annotation is drawn when printing.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a visible, printable annotation that is not a popup.</returns>
    private static bool IsPrintable(PdfDictionary annotation)
    {
        var flags = annotation.GetInt32(KnownName.F);
        return !annotation.IsName(KnownName.Subtype, KnownName.Popup) && (flags & HiddenFlag) == 0 && (flags & PrintFlag) != 0;
    }

    /// <summary>Gets the normal appearance stream an annotation shows.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="raw">The appearance as stored, so a shared one can be copied by reference.</param>
    /// <returns>The stream, or <see langword="null"/> when the annotation has none.</returns>
    private static PdfStream? GetAppearance(PdfDictionary annotation, out PdfValue raw)
    {
        raw = default;
        if (annotation.GetDictionary(KnownName.AP) is not { } ap)
        {
            return null;
        }

        var resolved = ap.Get(KnownName.N);
        if (resolved.AsStream() is { } direct)
        {
            raw = ap.GetRaw(KnownName.N);
            return direct;
        }

        return resolved.AsDictionary() is { } states ? PickState(annotation, states, out raw) : null;
    }

    /// <summary>Picks the appearance for the annotation's current state, or the first when it has none.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="states">The /N dictionary of states.</param>
    /// <param name="raw">The chosen appearance as stored.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    private static PdfStream? PickState(PdfDictionary annotation, PdfDictionary states, out PdfValue raw)
    {
        raw = default;
        var state = annotation.GetName(KnownName.AS);
        if (state.IsNone)
        {
            if (states.Count == 0)
            {
                return null;
            }

            raw = states.GetValueAt(0);
            return states.Get(states.GetKeyAt(0)).AsStream();
        }

        raw = states.GetRaw(state);
        return states.GetStream(state);
    }

    /// <summary>Gets the bounds an appearance covers once its matrix is applied.</summary>
    /// <param name="stream">The appearance stream.</param>
    /// <param name="bounds">The transformed box, normalised.</param>
    /// <returns><see langword="true"/> when the box has an area.</returns>
    private static bool TryGetAppearanceBounds(PdfStream stream, out PdfRectangle bounds)
    {
        var dictionary = stream.Dictionary;
        bounds = default;
        if (!dictionary.TryGetRectangle(KnownName.Rect, out var box) && !dictionary.TryGetRectangle(KnownName.BBox, out box))
        {
            return false;
        }

        var matrix = Matrix3x2.Identity;
        Span<float> values = stackalloc float[MatrixValues];
        if (dictionary.GetArray(KnownName.Matrix) is { } array && array.ReadNumbers(values) == MatrixValues)
        {
            matrix = new(values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        var a = Vector2.Transform(new(box.Left, box.Bottom), matrix);
        var b = Vector2.Transform(new(box.Right, box.Bottom), matrix);
        var c = Vector2.Transform(new(box.Right, box.Top), matrix);
        var d = Vector2.Transform(new(box.Left, box.Top), matrix);
        bounds = new(
            Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
        return !bounds.IsEmpty;
    }

    /// <summary>Appends the decoded content of a page, with a newline after each stream.</summary>
    /// <param name="page">The page.</param>
    /// <param name="wrap">Whether to wrap the content in a saved graphics state, so what is drawn after it starts clean.</param>
    /// <param name="output">The content being built.</param>
    private static void AppendContents(PdfPage page, bool wrap, ref PooledBuffer output)
    {
        if (wrap)
        {
            output.Write("q\n"u8);
        }

        var contents = page.Dictionary.Get(KnownName.Contents);
        var piece = default(PooledBuffer);
        try
        {
            if (contents.AsStream() is { } single)
            {
                AppendStream(single, ref piece, ref output);
            }
            else if (contents.AsArray() is { } array)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    if (array.Get(i).AsStream() is { } stream)
                    {
                        AppendStream(stream, ref piece, ref output);
                    }
                }
            }
        }
        finally
        {
            piece.Dispose();
        }

        if (wrap)
        {
            output.Write("Q\n"u8);
        }
    }

    /// <summary>Decodes a content stream and appends it.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="piece">Scratch space for the decoded bytes.</param>
    /// <param name="output">The content being built.</param>
    private static void AppendStream(PdfStream stream, ref PooledBuffer piece, ref PooledBuffer output)
    {
        piece.Length = 0;
        _ = stream.Decode(ref piece);
        output.Write(piece.WrittenSpan);
        output.WriteByte((byte)'\n');
    }

    /// <summary>Copies a page into the object reserved for it.</summary>
    /// <param name="page">The source page.</param>
    /// <param name="id">The reserved object.</param>
    /// <param name="prepared">The annotations prepared by the carrier, or <see langword="null"/> to copy the filtered ones.</param>
    /// <param name="annotations">The annotations kept when none are prepared.</param>
    /// <returns>The new page's id.</returns>
    private PdfObjectId ImportPageCore(PdfPage page, PdfObjectId id, PdfArray? prepared, PdfAnnotationFilter annotations)
    {
        var copy = new PdfDictionary(null);
        var source = page.Dictionary;
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (!key.Is(KnownName.Type) && !key.Is(KnownName.Parent) && !key.Is(KnownName.Annots))
            {
                copy.Add(CopyKey(key), _objects.Import(source.GetValueAt(i)));
            }
        }

        SetInheritedAttributes(copy, page);
        var annots = prepared is null ? ImportAnnotations(page, annotations) : ImportPrepared(prepared);
        if (annots.Count > 0)
        {
            copy.Set(KnownName.Annots, PdfValue.FromArray(annots));
        }

        return _target.AddPage(id, copy);
    }

    /// <summary>Appends the drawing of each flattened annotation.</summary>
    /// <param name="appearances">The annotations.</param>
    /// <param name="output">The content being built.</param>
    private void AppendAnnotations(List<FlattenedAnnotation> appearances, ref PooledBuffer output)
    {
        foreach (var appearance in appearances)
        {
            output.Write("q "u8);
            var m = appearance.Transform;
            foreach (var number in (ReadOnlySpan<float>)[m.M11, m.M12, m.M21, m.M22, m.M31, m.M32])
            {
                PdfSyntax.WriteNumber(ref output, number);
                output.WriteByte((byte)' ');
            }

            output.Write("cm "u8);
            PdfSyntax.WriteName(ref output, _target.Names.GetSpelling(appearance.Name));
            output.Write(" Do Q\n"u8);
        }
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

    /// <summary>Copies the annotations a filter keeps; each becomes an object of its own.</summary>
    /// <param name="page">The source page.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The references to the copies.</returns>
    private PdfArray ImportAnnotations(PdfPage page, PdfAnnotationFilter filter)
    {
        var annots = new PdfArray(null);
        var source = filter == PdfAnnotationFilter.None ? null : page.Dictionary.GetArray(KnownName.Annots);
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

    /// <summary>Finds the annotations that print, with where each one lands on the page.</summary>
    /// <param name="page">The source page.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="output">Receives each annotation's appearance, name and transform.</param>
    private void CollectFlattened(PdfPage page, PdfAnnotationFilter filter, List<FlattenedAnnotation> output)
    {
        var source = filter == PdfAnnotationFilter.None ? null : page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; source is not null && i < source.Count; i++)
        {
            if (source.GetDictionary(i) is { } annotation && Passes(annotation, filter) && IsPrintable(annotation)
                && TryFlatten(annotation, output.Count, out var flattened))
            {
                output.Add(flattened);
            }
        }
    }

    /// <summary>Works out how an annotation's appearance is drawn into its rectangle.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="index">The annotation's position among those flattened, for its resource name.</param>
    /// <param name="flattened">The appearance, name and transform.</param>
    /// <returns><see langword="true"/> when the annotation has a usable appearance.</returns>
    private bool TryFlatten(PdfDictionary annotation, int index, out FlattenedAnnotation flattened)
    {
        flattened = default;
        if (GetAppearance(annotation, out var raw) is not { } stream
            || !TryGetAppearanceBounds(stream, out var bounds)
            || !annotation.TryGetRectangle(KnownName.Rect, out var rect)
            || rect.IsEmpty)
        {
            return false;
        }

        var form = _objects.Import(raw);
        if (!form.IsReference)
        {
            return false;
        }

        var scaleX = rect.Width / bounds.Width;
        var scaleY = rect.Height / bounds.Height;
        var transform = new Matrix3x2(scaleX, 0, 0, scaleY, rect.Left - (bounds.Left * scaleX), rect.Bottom - (bounds.Bottom * scaleY));
        var name = _target.Names.Intern(string.Create(CultureInfo.InvariantCulture, $"{AnnotationFormPrefix}{index}"));
        flattened = new(name, form, transform);
        return true;
    }

    /// <summary>Builds a page's resources for its form, with the flattened annotations added to its XObjects.</summary>
    /// <param name="page">The source page.</param>
    /// <param name="appearances">The annotations drawn into the form.</param>
    /// <returns>The resources.</returns>
    private PdfValue BuildResources(PdfPage page, List<FlattenedAnnotation> appearances)
    {
        var resources = page.Resources ?? new PdfDictionary(null);
        if (appearances.Count == 0)
        {
            return _objects.Import(PdfValue.FromDictionary(resources));
        }

        var copy = new PdfDictionary(null, resources.Count + 1);
        for (var i = 0; i < resources.Count; i++)
        {
            var key = resources.GetKeyAt(i);
            if (!key.Is(KnownName.XObject))
            {
                copy.Add(CopyKey(key), _objects.Import(resources.GetValueAt(i)));
            }
        }

        var xobjects = new PdfDictionary(null);
        if (resources.GetDictionary(KnownName.XObject) is { } existing)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                xobjects.Add(CopyKey(existing.GetKeyAt(i)), _objects.Import(existing.GetValueAt(i)));
            }
        }

        foreach (var appearance in appearances)
        {
            xobjects.Set(appearance.Name, appearance.Form);
        }

        copy.Set(KnownName.XObject, PdfValue.FromDictionary(xobjects));
        return PdfValue.FromDictionary(copy);
    }

    /// <summary>An annotation appearance drawn into a form.</summary>
    /// <param name="Name">The resource name the form is drawn by.</param>
    /// <param name="Form">A reference to the appearance in the new document.</param>
    /// <param name="Transform">Maps the appearance onto the annotation's rectangle.</param>
    [DebuggerDisplay("FlattenedAnnotation: {Name}")]
    private readonly record struct FlattenedAnnotation(PdfName Name, PdfValue Form, Matrix3x2 Transform);
}
