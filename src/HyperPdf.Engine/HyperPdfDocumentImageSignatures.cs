// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentImageSignatures over the document's owned state.</summary>
internal static class HyperPdfDocumentImageSignatures
{
    /// <summary>Copies an image into a removable signature annotation.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="bounds">The image bounds in page points, with a top-left origin.</param>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels, in rows from top to bottom.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The annotation index, or -1 when placement fails.</returns>
    internal static int AddImageSignature(HyperPdfDocument self, int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationImages.AddImageSignature(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, bounds, pixels, width, height));
        }
    }
}
