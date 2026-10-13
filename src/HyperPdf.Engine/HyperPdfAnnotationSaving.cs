// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationSaving annotation operations.</summary>
internal static class HyperPdfAnnotationSaving
{
    /// <summary>
    /// Saves the document to a stream. Documents with digital signatures are saved incrementally so the signatures stay
    /// valid; others are saved compactly.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim.</param>
    /// <returns>
    /// <see langword="true" /> when saved.</returns>
    internal static bool Save(HyperPdfAnnotations annotationState, Stream destination, PdfSaveReport report)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (annotationState.Gate)
        {
            if (annotationState.Document.IsDisposed)
            {
                return false;
            }

            var hidden = HideRemoved(annotationState);
            try
            {
                if (SignaturePresence.HasSignature(annotationState.Store))
                {
                    PdfIncrementalWriter.Save(annotationState.Store, destination, report);
                }
                else
                {
                    PdfCompactWriter.Save(annotationState.Store, PdfCompactOptions.Default, destination, report);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (PdfException exception)
            {
                SaveFailureMapping.Note(report, exception);
                return false;
            }
            finally
            {
                Restore(annotationState, hidden);
            }

            Volatile.Write(ref annotationState.UnsavedChanges, 0);
            return true;
        }
    }

    /// <summary>Builds a stable save under the annotation lock, then returns its pending output write.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="destination">The output stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim.</param>
    /// <param name="cancellationToken">Cancels building and writing.</param>
    /// <param name="write">The pending write when the document is open.</param>
    /// <returns>Whether a save was started.</returns>
    internal static bool TryStartSaveAsync(HyperPdfAnnotations annotationState, Stream destination, PdfSaveReport report, CancellationToken cancellationToken, out Task write)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        write = Task.CompletedTask;
        lock (annotationState.Gate)
        {
            if (annotationState.Document.IsDisposed)
            {
                return false;
            }

            var hidden = HideRemoved(annotationState);
            try
            {
                // Both writers build their detached output before their first await.
                write = SignaturePresence.HasSignature(annotationState.Store)
                    ? PdfIncrementalWriter.SaveAsync(annotationState.Store, destination, report, cancellationToken)
                    : PdfCompactWriter.SaveAsync(annotationState.Store, PdfCompactOptions.Default, destination, report, cancellationToken);
            }
            finally
            {
                Restore(annotationState, hidden);
            }

            return true;
        }
    }

    /// <summary>Takes annotations removed but kept off their pages for saving.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The pages changed and their full annotation arrays, to put back.</returns>
    internal static RemovedAnnotationPage[] HideRemoved(HyperPdfAnnotations annotationState)
    {
        // Most documents have none removed, so nothing is allocated for them.
        if (annotationState.RemovedPageCount == 0)
        {
            return [];
        }

        var hidden = new RemovedAnnotationPage[annotationState.RemovedPageCount];
        var count = 0;
        foreach (var pageIndex in annotationState.PagesWithRemoved)
        {
            if (!TryHide(annotationState, pageIndex, out var page))
            {
                continue;
            }

            hidden[count] = page;
            count++;
        }

        return count == hidden.Length ? hidden : hidden[..count];
    }

    /// <summary>Puts back the annotations taken off for saving.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="hidden">The pages and their full arrays.</param>
    internal static void Restore(HyperPdfAnnotations annotationState, RemovedAnnotationPage[] hidden)
    {
        foreach (var (page, annotations) in hidden)
        {
            _ = PdfPageAnnotations.SetArray(annotationState.Store, page, annotations);
        }
    }

    /// <summary>Takes the removed annotations off one page, leaving the rest.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="hidden">The page and its full annotation array, to put back.</param>
    /// <returns><see langword="true"/> when the page's annotations were changed.</returns>
    private static bool TryHide(HyperPdfAnnotations annotationState, int pageIndex, out RemovedAnnotationPage hidden)
    {
        hidden = default;
        if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page || PdfPageAnnotations.GetArray(annotationState.Store, page) is not { } annotations)
        {
            return false;
        }

        // A page whose removed annotations are no longer in its array needs no copy.
        if (!HasRemoved(annotationState, annotations))
        {
            return false;
        }

        var kept = new PdfArray(annotationState.Store, annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation || !HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation))
            {
                kept.Add(annotations.GetRaw(i));
            }
        }

        hidden = new(page, annotations);
        return PdfPageAnnotations.SetArray(annotationState.Store, page, kept);
    }

    /// <summary>Determines whether a page's annotation array holds an annotation that was removed but kept.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotations">The page's annotation array.</param>
    /// <returns><see langword="true"/> when one is in the array.</returns>
    private static bool HasRemoved(HyperPdfAnnotations annotationState, PdfArray annotations)
    {
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation))
            {
                return true;
            }
        }

        return false;
    }
}
