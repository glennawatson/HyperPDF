// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Implements Optimization over the document's owned state.</summary>
internal static class HyperPdfOptimization
{
    /// <summary>Copies the open document with its unsaved edits while no edit or save runs, so the optimiser works without holding the edit gate.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ObjectDisposedException">The document was closed.</exception>
    internal static PdfDocument TakeSnapshot(HyperPdfDocument self)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfAnnotationOptimization.OpenSnapshot(HyperPdfAnnotationStateAccess.GetAnnotations(self)) ?? throw new ObjectDisposedException(self.GetType().FullName);
        }
    }
}
