// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Moves a page's annotations with its content when the content is transformed: the rectangle and the point lists of each
/// annotation follow the matrix, and each appearance stream is drawn through the matrix's linear part inside a form of its
/// own, because a reader fits an appearance to its annotation's rectangle by scale and offset alone (ISO 32000-2 12.5.5).
/// </summary>
internal static class PdfAnnotationTransform
{
    /// <summary>The number of values in a transformation matrix.</summary>
    private const int MatrixValues = 6;

    /// <summary>The numbers in a point: x and y.</summary>
    private const int PointStride = 2;

    /// <summary>Gets the resource name of the wrapped appearance inside its form.</summary>
    private static ReadOnlySpan<byte> AppearanceName => "Ap"u8;

    /// <summary>Gets the keys that hold a list of x and y values.</summary>
    private static ReadOnlySpan<KnownName> PointLists => [KnownName.QuadPoints, KnownName.Vertices, KnownName.L, KnownName.CL];

    /// <summary>Transforms the annotations of a page and returns the array that replaces its /Annots.</summary>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="annotations">The page's /Annots array.</param>
    /// <param name="matrix">The matrix the content is transformed by.</param>
    /// <returns>The new array; annotations that are objects of their own keep their references.</returns>
    internal static PdfArray Transform(PdfEditTransaction transaction, PdfObjectStore store, PdfArray annotations, Matrix3x2 matrix)
    {
        var result = new PdfArray(store, annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            var raw = annotations.GetRaw(i);
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                result.Add(raw);
                continue;
            }

            var copy = annotation.Clone();
            TransformAnnotation(transaction, store, copy, matrix);
            if (raw.IsReference)
            {
                transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
                result.Add(raw);
            }
            else
            {
                result.Add(PdfValue.FromDictionary(copy));
            }
        }

        return result;
    }

    /// <summary>Gets the smallest rectangle that holds a rectangle after a transform.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The bounds of the transformed corners.</returns>
    internal static PdfRectangle GetBounds(PdfRectangle rectangle, Matrix3x2 matrix)
    {
        var a = Vector2.Transform(new(rectangle.Left, rectangle.Bottom), matrix);
        var b = Vector2.Transform(new(rectangle.Right, rectangle.Bottom), matrix);
        var c = Vector2.Transform(new(rectangle.Right, rectangle.Top), matrix);
        var d = Vector2.Transform(new(rectangle.Left, rectangle.Top), matrix);
        return new(
            Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }

    /// <summary>Changes one annotation dictionary to follow the matrix.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="annotation">The copy of the annotation to change.</param>
    /// <param name="matrix">The matrix.</param>
    private static void TransformAnnotation(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary annotation, Matrix3x2 matrix)
    {
        if (annotation.TryGetRectangle(KnownName.Rect, out var rect))
        {
            annotation.Set(KnownName.Rect, PdfValue.FromArray(GetBounds(rect, matrix).ToArray(store)));
        }

        foreach (var key in PointLists)
        {
            if (annotation.GetArray(key) is { } points)
            {
                annotation.Set(key, PdfValue.FromArray(TransformPoints(store, points, matrix)));
            }
        }

        if (annotation.GetArray(KnownName.InkList) is { } strokes)
        {
            var moved = new PdfArray(store, strokes.Count);
            for (var i = 0; i < strokes.Count; i++)
            {
                moved.Add(strokes.GetArray(i) is { } stroke ? PdfValue.FromArray(TransformPoints(store, stroke, matrix)) : strokes.GetRaw(i));
            }

            annotation.Set(KnownName.InkList, PdfValue.FromArray(moved));
        }

        if (annotation.GetDictionary(KnownName.AP) is { } appearances)
        {
            annotation.Set(KnownName.AP, PdfValue.FromDictionary(TransformAppearances(transaction, store, appearances, matrix)));
        }
    }

    /// <summary>Transforms a list of x and y values as points.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="points">The list.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The transformed list; a trailing unpaired value is kept.</returns>
    private static PdfArray TransformPoints(PdfObjectStore store, PdfArray points, Matrix3x2 matrix)
    {
        var values = new float[points.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (float)points.GetNumber(i);
        }

        for (var i = 0; i + 1 < values.Length; i += PointStride)
        {
            var moved = Vector2.Transform(new(values[i], values[i + 1]), matrix);
            values[i] = moved.X;
            values[i + 1] = moved.Y;
        }

        return PdfArray.FromNumbers(store, values);
    }

    /// <summary>Wraps the appearance streams of an annotation so they are drawn through the matrix.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="appearances">The annotation's /AP dictionary.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The changed copy.</returns>
    private static PdfDictionary TransformAppearances(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary appearances, Matrix3x2 matrix)
    {
        var copy = appearances.Clone();
        for (var i = 0; i < copy.Count; i++)
        {
            var key = copy.GetKeyAt(i);
            var value = copy.GetValueAt(i);
            if (copy.Get(key).AsStream() is { } stream)
            {
                copy.Set(key, Wrap(transaction, store, value, stream, matrix));
            }
            else if (copy.Get(key).AsDictionary() is { } states)
            {
                copy.Set(key, PdfValue.FromDictionary(WrapStates(transaction, store, states, matrix)));
            }
        }

        return copy;
    }

    /// <summary>Wraps each appearance stream of a dictionary of appearance states.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="states">The dictionary, such as <c>/On</c> and <c>/Off</c>.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The changed copy.</returns>
    private static PdfDictionary WrapStates(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary states, Matrix3x2 matrix)
    {
        var copy = states.Clone();
        for (var i = 0; i < copy.Count; i++)
        {
            if (copy.Get(copy.GetKeyAt(i)).AsStream() is { } stream)
            {
                copy.Set(copy.GetKeyAt(i), Wrap(transaction, store, copy.GetValueAt(i), stream, matrix));
            }
        }

        return copy;
    }

    /// <summary>Makes a form that draws an appearance stream through the linear part of a matrix.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="appearance">The appearance as stored: a reference to the stream.</param>
    /// <param name="stream">The appearance stream.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>A reference to the new form, or the appearance unchanged when it has no usable box.</returns>
    private static PdfValue Wrap(PdfEditTransaction transaction, PdfObjectStore store, PdfValue appearance, PdfStream stream, Matrix3x2 matrix)
    {
        if (!appearance.IsReference || !stream.Dictionary.TryGetRectangle(KnownName.BBox, out var box))
        {
            return appearance;
        }

        var linear = new Matrix3x2(matrix.M11, matrix.M12, matrix.M21, matrix.M22, 0, 0);
        var bounds = GetBounds(box, ReadMatrix(stream.Dictionary) * linear);
        var forms = new PdfDictionary(store);
        forms.Set(store.Names.Intern(AppearanceName), appearance);
        var resources = new PdfDictionary(store);
        resources.Set(KnownName.XObject, PdfValue.FromDictionary(forms));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.Transform(linear.M11, linear.M12, linear.M21, linear.M22, 0, 0);
            builder.DrawXObject(AppearanceName);
            return PdfValue.FromReference(transaction.Add(PdfValue.FromStream(builder.ToFormXObject(store, bounds, resources))));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Reads a form's <c>/Matrix</c>.</summary>
    /// <param name="form">The form dictionary.</param>
    /// <returns>The matrix, or the identity.</returns>
    private static Matrix3x2 ReadMatrix(PdfDictionary form)
    {
        Span<float> values = stackalloc float[MatrixValues];
        return form.GetArray(KnownName.Matrix) is { } array && array.ReadNumbers(values) == MatrixValues
            ? new(values[0], values[1], values[2], values[3], values[4], values[5])
            : Matrix3x2.Identity;
    }
}
