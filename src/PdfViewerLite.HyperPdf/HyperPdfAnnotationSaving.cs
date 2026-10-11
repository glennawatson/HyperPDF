// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace PdfViewerLite.HyperPdf;

/// <summary>Performs HyperPdfAnnotationSaving annotation operations.</summary>
internal static class HyperPdfAnnotationSaving
{
    /// <summary>How deep the form field tree is searched for signatures.</summary>
    internal const int MaxFieldDepth = 32;

    /// <summary>
    /// Saves the document to a stream. Documents with digital signatures are saved incrementally so the signatures stay
    /// valid; others are saved compactly.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <returns>
    /// <see langword="true" /> when saved.</returns>
    internal static bool Save(HyperPdfAnnotations annotationState, Stream destination)
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
                if (HasSignedFields(annotationState.Store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields), 0))
                {
                    PdfIncrementalWriter.Save(annotationState.Store, destination);
                }
                else
                {
                    PdfCompactWriter.Save(annotationState.Store, PdfCompactOptions.Default, destination);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (PdfException)
            {
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
    /// <param name="cancellationToken">Cancels building and writing.</param>
    /// <param name="write">The pending write when the document is open.</param>
    /// <returns>Whether a save was started.</returns>
    internal static bool TryStartSaveAsync(HyperPdfAnnotations annotationState, Stream destination, CancellationToken cancellationToken, out Task write)
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
                write = HasSignedFields(annotationState.Store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields), 0)
                    ? PdfIncrementalWriter.SaveAsync(annotationState.Store, destination, cancellationToken)
                    : PdfCompactWriter.SaveAsync(annotationState.Store, PdfCompactOptions.Default, destination, cancellationToken);
            }
            finally
            {
                Restore(annotationState, hidden);
            }

            return true;
        }
    }

    /// <summary>Determines whether a field list holds a signed signature field: <c>/FT /Sig</c> with a <c>/V</c>.</summary>
    /// <param name="fields">The fields, or their kids.</param>
    /// <param name="depth">How deep in the field tree the list is.</param>
    /// <returns><see langword="true"/> when one is signed.</returns>
    internal static bool HasSignedFields(PdfArray? fields, int depth)
    {
        if (fields is null || depth > MaxFieldDepth)
        {
            return false;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is not { } field)
            {
                continue;
            }

            if ((field.IsName(KnownName.FT, KnownName.Sig) && !field.Get(KnownName.V).IsNull) || HasSignedFields(field.GetArray(KnownName.Kids), depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes annotations removed but kept off their pages for saving.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The pages changed and their full annotation arrays, to put back.</returns>
    internal static List<RemovedPage> HideRemoved(HyperPdfAnnotations annotationState)
    {
        var hidden = new List<RemovedPage>(annotationState.PagesWithRemoved.Count);
        foreach (var pageIndex in annotationState.PagesWithRemoved)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page || PdfPageAnnotations.GetArray(annotationState.Store, page) is not { } annotations)
            {
                continue;
            }

            var kept = new PdfArray(annotationState.Store, annotations.Count);
            for (var i = 0; i < annotations.Count; i++)
            {
                if (annotations.GetDictionary(i) is not { } annotation || !HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation))
                {
                    kept.Add(annotations.GetRaw(i));
                }
            }

            if (kept.Count != annotations.Count && PdfPageAnnotations.SetArray(annotationState.Store, page, kept))
            {
                hidden.Add(new(page, annotations));
            }
        }

        return hidden;
    }

    /// <summary>Puts back the annotations taken off for saving.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="hidden">The pages and their full arrays.</param>
    internal static void Restore(HyperPdfAnnotations annotationState, List<RemovedPage> hidden)
    {
        foreach (var (page, annotations) in hidden)
        {
            _ = PdfPageAnnotations.SetArray(annotationState.Store, page, annotations);
        }
    }

    /// <summary>A page whose removed annotations were taken off for saving, and its full annotation array.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Annotations">The full array.</param>
    internal sealed record RemovedPage(PdfPage Page, PdfArray Annotations);
}
