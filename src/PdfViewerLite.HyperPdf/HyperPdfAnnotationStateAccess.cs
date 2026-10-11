// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf;

/// <summary>Implements AnnotationStateAccess over the document's owned state.</summary>
internal static class HyperPdfAnnotationStateAccess
{
    /// <summary>Gets Annotations.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static HyperPdfAnnotations GetAnnotations(HyperPdfDocument self)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        return Volatile.Read(ref self.AnnotationState)
            ?? Interlocked.CompareExchange(ref self.AnnotationState, new(self.Document, self.AnnotationClock, self.FontState), null)
            ?? Volatile.Read(ref self.AnnotationState)!;
    }
}
