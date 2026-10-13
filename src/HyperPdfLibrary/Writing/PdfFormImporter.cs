// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Copies a page of one document as a form XObject (ISO 32000-2 8.10) into a target that a <see cref="IPdfCarrySink"/>
/// describes: a new document through its builder or an open document through a transaction. Printable annotations are
/// flattened into the form.
/// </summary>
/// <remarks>Not thread-safe. The source document must stay open until the target is saved.</remarks>
[DebuggerDisplay("PdfFormImporter: {_sink}")]
internal sealed class PdfFormImporter
{
    /// <summary>The annotation flag that hides an annotation.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The annotation flag that prints an annotation.</summary>
    private const int PrintFlag = 1 << 2;

    /// <summary>The prefix of the names given to flattened annotation forms.</summary>
    private const string AnnotationFormPrefix = "PdfFlat";

    /// <summary>The number of values in a transformation matrix.</summary>
    private const int MatrixValues = 6;

    /// <summary>Receives the copies and copies the objects they refer to.</summary>
    private readonly IPdfCarrySink _sink;

    /// <summary>Initializes a new instance of the <see cref="PdfFormImporter"/> class.</summary>
    /// <param name="sink">The target and the importer that copies source values into it.</param>
    internal PdfFormImporter(IPdfCarrySink sink) => _sink = sink;

    /// <summary>
    /// Copies a page as a form XObject: its content, then its printable annotations, drawn in the page's user space.
    /// The form's box is <paramref name="box"/>, which clips the content. An optional content group can control it
    /// (<c>/OC</c>, ISO 32000-2 8.11.3.3).
    /// </summary>
    /// <param name="page">The source page.</param>
    /// <param name="box">The part of the page the form shows, in user space.</param>
    /// <param name="annotations">The annotations drawn into the form.</param>
    /// <param name="applyRotation">Whether the form's matrix turns the page upright; otherwise the form keeps user space.</param>
    /// <param name="group">The optional content group in the target, or an invalid id for none.</param>
    /// <returns>The form's id.</returns>
    internal PdfObjectId ImportForm(PdfPage page, in PdfRectangle box, PdfAnnotationFilter annotations, bool applyRotation, PdfObjectId group)
    {
        var appearances = new List<FlattenedAnnotation>();
        CollectFlattened(page, annotations, appearances);
        var form = new PdfDictionary(null);
        form.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        form.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        form.Set(KnownName.BBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [box.Left, box.Bottom, box.Right, box.Top])));
        if (applyRotation)
        {
            var matrix = PdfPageImporter.GetPageMatrix(box, page.Rotation);
            form.Set(KnownName.Matrix, PdfValue.FromArray(PdfArray.FromNumbers(null, [matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32])));
        }

        if (group.IsValid)
        {
            form.Set(KnownName.OC, PdfValue.FromReference(group));
        }

        form.Set(KnownName.Resources, BuildResources(page, appearances));
        var content = default(PooledBuffer);
        try
        {
            AppendContents(page, appearances.Count > 0, ref content);
            AppendAnnotations(appearances, ref content);
            var id = _sink.Reserve();
            _sink.Set(id, PdfValue.FromStream(new(form, content.ToArray())));
            return id;
        }
        finally
        {
            content.Dispose();
        }
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
            PdfSyntax.WriteName(ref output, _sink.TargetNames.GetSpelling(appearance.Name));
            output.Write(" Do Q\n"u8);
        }
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
            if (source.GetDictionary(i) is { } annotation && PdfPageImporter.Passes(annotation, filter) && IsPrintable(annotation)
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

        var form = _sink.Import(raw);
        if (!form.IsReference)
        {
            return false;
        }

        var scaleX = rect.Width / bounds.Width;
        var scaleY = rect.Height / bounds.Height;
        var transform = new Matrix3x2(scaleX, 0, 0, scaleY, rect.Left - (bounds.Left * scaleX), rect.Bottom - (bounds.Bottom * scaleY));
        var name = _sink.TargetNames.Intern(string.Create(CultureInfo.InvariantCulture, $"{AnnotationFormPrefix}{index}"));
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
            return _sink.Import(PdfValue.FromDictionary(resources));
        }

        var copy = new PdfDictionary(null, resources.Count + 1);
        for (var i = 0; i < resources.Count; i++)
        {
            var key = resources.GetKeyAt(i);
            if (!key.Is(KnownName.XObject))
            {
                copy.Add(_sink.ImportName(key), _sink.Import(resources.GetValueAt(i)));
            }
        }

        var xobjects = new PdfDictionary(null);
        if (resources.GetDictionary(KnownName.XObject) is { } existing)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                xobjects.Add(_sink.ImportName(existing.GetKeyAt(i)), _sink.Import(existing.GetValueAt(i)));
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
