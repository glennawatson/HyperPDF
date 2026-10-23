// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Composition;

/// <summary>Makes the destination arrays that links, names and outline entries use to jump to a place on a page.</summary>
internal static class DestinationArrays
{
    /// <summary>The items in a destination array: the page, the fit name, and the left, top and zoom values.</summary>
    private const int ArrayLength = 5;

    /// <summary>Makes a destination that shows the page with a point at the top left of the window and the zoom unchanged.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="page">The page's object.</param>
    /// <param name="point">The point.</param>
    /// <returns>The array.</returns>
    internal static PdfValue Create(PdfDocumentBuilder builder, PdfObjectId page, DestinationPoint point)
    {
        var array = new PdfArray(builder.Owner, ArrayLength);
        array.Add(PdfValue.FromReference(page));
        array.Add(PdfValue.FromName(KnownName.XYZ));
        array.Add(PdfValue.FromReal(point.X));
        array.Add(PdfValue.FromReal(point.Y));
        array.Add(PdfValue.FromInteger(0));
        return PdfValue.FromArray(array);
    }
}
