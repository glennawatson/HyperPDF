// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationOptimization annotation operations.</summary>
internal static class HyperPdfAnnotationOptimization
{
    /// <summary>Takes a snapshot of the document with its unsaved edits, leaving out annotations removed but kept.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the document is closed.</returns>
    internal static PdfDocument? OpenSnapshot(HyperPdfAnnotations annotationState)
    {
        lock (annotationState.Gate)
        {
            if (annotationState.Document.IsDisposed)
            {
                return null;
            }

            var hidden = HyperPdfAnnotationSaving.HideRemoved(annotationState);
            try
            {
                return PdfDocumentOptimizing.OpenWorkingCopy(annotationState.Document);
            }
            finally
            {
                HyperPdfAnnotationSaving.Restore(annotationState, hidden);
            }
        }
    }
}
