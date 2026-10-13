// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Turns a page's <c>/Rotate</c> into its content (ISO 32000-2 8.3.4, 8.4.4): the content and the annotations that stay on
/// the page are moved so the page shows the same upright, the page boxes follow, and the rotation becomes 0.
/// </summary>
internal static class PdfFlattenRotation
{
    /// <summary>Bakes the rotation of a page into the copy of its dictionary.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page as it was before the edit.</param>
    /// <param name="copy">The copy of the page dictionary being changed.</param>
    internal static void Bake(PdfFlattenSession session, PdfPage page, PdfDictionary copy)
    {
        var store = session.Document.Objects;
        var matrix = PdfPageImporter.GetPageMatrix(page.CropBox, page.Rotation);
        PdfPageStamping.Wrap(session.Transaction, store, copy, WritePrefix(matrix), "Q\n"u8);
        if (copy.GetArray(KnownName.Annots) is { Count: > 0 } annotations)
        {
            copy.Set(KnownName.Annots, PdfValue.FromArray(PdfAnnotationTransform.Transform(session.Transaction, store, annotations, matrix)));
        }

        SetBox(copy, store, KnownName.MediaBox, page.MediaBox, matrix, true);
        SetBox(copy, store, KnownName.CropBox, page.CropBox, matrix, true);
        SetBox(copy, store, KnownName.BleedBox, page.BleedBox, matrix, false);
        SetBox(copy, store, KnownName.TrimBox, page.TrimBox, matrix, false);
        SetBox(copy, store, KnownName.ArtBox, page.ArtBox, matrix, false);

        // An explicit 0 also overrides a rotation the page tree hands down.
        copy.Set(KnownName.Rotate, PdfValue.FromInteger(0));
    }

    /// <summary>Writes the operators that open the turned content.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>A saved graphics state and the matrix.</returns>
    private static byte[] WritePrefix(Matrix3x2 matrix)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes a page box moved by the matrix.</summary>
    /// <param name="copy">The page dictionary copy.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="key">The box's key.</param>
    /// <param name="box">The box as the page reads it.</param>
    /// <param name="matrix">The matrix.</param>
    /// <param name="always">Whether the box is written even when the page did not hold it, as it may be inherited.</param>
    private static void SetBox(PdfDictionary copy, PdfObjectStore store, KnownName key, in PdfRectangle box, Matrix3x2 matrix, bool always)
    {
        if (always || copy.ContainsKey(key))
        {
            copy.Set(key, PdfValue.FromArray(PdfAnnotationTransform.GetBounds(box, matrix).ToArray(store)));
        }
    }
}
