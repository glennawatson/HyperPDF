// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text.Fonts;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationReading annotation operations.</summary>
internal static class HyperPdfAnnotationReading
{
    /// <summary>The "print" flag, so annotations appear on paper too.</summary>
    internal const PdfAnnotationFlags PrintFlags = PdfAnnotationFlags.Print;

    /// <summary>Gets HasUnsavedChanges.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The current value.</returns>
    internal static bool GetHasUnsavedChanges(HyperPdfAnnotations annotationState) => Volatile.Read(ref annotationState.UnsavedChanges) != 0;

    /// <summary>Gets Author.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetAuthor(HyperPdfAnnotations annotationState) => Volatile.Read(ref annotationState.Author);

    /// <summary>Sets Author.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="value">The value to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetAuthor(HyperPdfAnnotations annotationState, string value) => Volatile.Write(ref annotationState.Author, string.IsNullOrWhiteSpace(value)
        ? Environment.UserName
        : value.Trim());

    /// <summary>Gets FontCatalog.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <returns>The current value.</returns>
    internal static FontCatalog GetFontCatalog(HyperPdfAnnotations annotationState)
    {
        lock (annotationState.Gate)
        {
            if (annotationState.Catalog is { } selected)
            {
                return selected;
            }
        }

        return annotationState.LazyFontState.Value.Catalog;
    }

    /// <summary>Sets FontCatalog.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="value">The value to use.</param>
    internal static void SetFontCatalog(HyperPdfAnnotations annotationState, FontCatalog value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (annotationState.Gate)
        {
            annotationState.Catalog = value;
        }
    }

    /// <summary>Appends the annotations on a page, skipping links, form fields and pop-ups.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the annotations.</param>
    internal static void GetAnnotations(HyperPdfAnnotations annotationState, int pageIndex, List<PageAnnotation> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (annotationState.Gate)
        {
            if (annotationState.Cache.TryGetValue(pageIndex, out var cached))
            {
                output.AddRange(cached);
                return;
            }

            if (GetPage(annotationState, pageIndex) is not { } page)
            {
                return;
            }

            var start = output.Count;
            Read(annotationState, page, output);
            annotationState.Cache[pageIndex] = [.. CollectionsMarshal.AsSpan(output)[start..]];
        }
    }

    /// <summary>Changes an annotation's note text.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="contents">The note text.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetContents(HyperPdfAnnotations annotationState, int pageIndex, int index, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        lock (annotationState.Gate)
        {
            if (!TryEdit(annotationState, pageIndex, index, out var page, out var annotation))
            {
                return false;
            }

            PdfAnnotations.SetText(annotation, KnownName.Contents, contents);
            SetModified(annotationState, annotation);
            return Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>
    /// Takes an annotation off the page, or puts it back. A removed annotation keeps its index, is no longer listed or
    /// drawn, and is left out when the document is saved, so removing can be undone even after saving.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="removed">Whether the annotation is removed.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetRemoved(HyperPdfAnnotations annotationState, int pageIndex, int index, bool removed)
    {
        lock (annotationState.Gate)
        {
            if (!TryEdit(annotationState, pageIndex, index, out var page, out var annotation) || HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation) == removed)
            {
                return false;
            }

            var flags = PdfAnnotations.GetFlags(annotation);
            if (removed)
            {
                PdfAnnotations.SetNumberText(annotation, annotationState.Names.Removed, (int)flags);
                PdfAnnotations.SetFlags(annotation, flags | PdfAnnotationFlags.Hidden);
                _ = annotationState.PagesWithRemoved.Add(pageIndex);
            }
            else
            {
                PdfAnnotations.SetFlags(annotation, (PdfAnnotationFlags)(int)PdfAnnotations.GetNumberText(annotation, annotationState.Names.Removed));
                _ = annotation.Remove(annotationState.Names.Removed);
            }

            return Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>Removes an annotation for good; the indexes of later annotations on the page shift down.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>
    /// <see langword="true" /> when removed.</returns>
    internal static bool Remove(HyperPdfAnnotations annotationState, int pageIndex, int index)
    {
        lock (annotationState.Gate)
        {
            return GetPage(annotationState, pageIndex) is { } page && Changed(annotationState, pageIndex, PdfPageAnnotations.RemoveAt(annotationState.Store, page, index));
        }
    }

    /// <summary>Forgets page-indexed snapshots and finds removed annotations in the current page order.</summary>
    /// <param name="annotationState">The annotation state.</param>
    internal static void InvalidatePages(HyperPdfAnnotations annotationState)
    {
        lock (annotationState.Gate)
        {
            annotationState.ForgetPages();
            for (var pageIndex = 0; pageIndex < annotationState.Document.PageCount; pageIndex++)
            {
                if (GetPage(annotationState, pageIndex) is not { } page || PdfPageAnnotations.GetArray(annotationState.Store, page) is not { } annotations)
                {
                    continue;
                }

                for (var index = 0; index < annotations.Count; index++)
                {
                    if (annotations.GetDictionary(index) is not { } annotation || !HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation))
                    {
                        continue;
                    }

                    _ = annotationState.PagesWithRemoved.Add(pageIndex);
                    break;
                }
            }
        }
    }

    /// <summary>Converts a page point to user space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 ToUser(PdfPage page, PagePoint point) => page.FromPhysical(new(point.X, point.Y));

    /// <summary>Converts a user space rectangle to page space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The page rectangle, in physical points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PageRect ToPageRect(PdfPage page, PdfRectangle rectangle) => LinkTargets.ToPageRect(page.ToPhysicalRectangle(rectangle));

    /// <summary>Converts a page rectangle to user space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page space.</param>
    /// <returns>The user space rectangle.</returns>
    internal static PdfRectangle ToUserRectangle(PdfPage page, PageRect bounds)
    {
        var a = ToUser(page, new(bounds.Left, bounds.Top));
        var b = ToUser(page, new(bounds.Right, bounds.Bottom));
        return PdfRectangle.FromCorners(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Records the current time as the modification date.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetModified(HyperPdfAnnotations annotationState, PdfDictionary annotation) => PdfAnnotations.SetDate(annotation, KnownName.M, annotationState.Clock.GetUtcNow());

    /// <summary>Gets a page, or <see langword="null"/> when the index is out of range or the document is closed.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page.</returns>
    internal static PdfPage? GetPage(HyperPdfAnnotations annotationState, int pageIndex) =>
        annotationState.Document.IsDisposed || (uint)pageIndex >= (uint)annotationState.Document.PageCount ? null : PdfDocumentPages.GetPage(annotationState.Document, pageIndex);

    /// <summary>Appends the annotations on a page, skipping links, form fields, pop-ups, replies and removed ones.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="page">The page.</param>
    /// <param name="output">The list.</param>
    internal static void Read(HyperPdfAnnotations annotationState, PdfPage page, List<PageAnnotation> output)
    {
        if (PdfPageAnnotations.GetArray(annotationState.Store, page) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation || HyperPdfAnnotationKinds.GetKind(annotationState, annotation) is not { } kind
                || HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation) || HyperPdfAnnotationKinds.IsReply(annotationState, annotation))
            {
                continue;
            }

            var bounds = ToPageRect(page, PdfAnnotations.GetRectangle(annotation));
            output.Add(new(
                page.Index,
                i,
                kind,
                bounds,
                HyperPdfAnnotationKinds.GetColor(annotationState, annotation, kind),
                PdfAnnotations.GetText(annotation, KnownName.Contents),
                PdfAnnotations.GetText(annotation, KnownName.T))
            {
                LineWidth = PdfAnnotations.GetBorderWidth(annotation) * page.UserUnit,
                FontSize = PdfAnnotations.GetNumberText(annotation, annotationState.Names.FontSize) * page.UserUnit,
                Modified = PdfAnnotations.GetDate(annotation, KnownName.M),
            });
        }
    }

    /// <summary>Copies an annotation for editing.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The copy to edit.</param>
    /// <returns><see langword="true"/> when the annotation exists.</returns>
    internal static bool TryEdit(HyperPdfAnnotations annotationState, int pageIndex, int index, out PdfPage page, out PdfDictionary annotation)
    {
        page = GetPage(annotationState, pageIndex)!;
        annotation = page is null ? null! : PdfPageAnnotations.Get(annotationState.Store, page, index)?.Clone()!;
        return annotation is not null;
    }

    /// <summary>Puts an edited annotation back and records the edit.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="annotation">The edited annotation.</param>
    /// <returns><see langword="true"/> when put back.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Commit(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, int index, PdfDictionary annotation) =>
        Changed(annotationState, pageIndex, PdfPageAnnotations.Replace(annotationState.Store, page, index, annotation));

    /// <summary>Fills in what every new annotation records, then adds it to the page.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation, with its rectangle set.</param>
    /// <param name="color">The colour, or <see langword="null"/> for an appearance that keeps its own colours, such as a picture.</param>
    /// <param name="contents">The note text, or empty.</param>
    /// <param name="subject">The <c>/Subj</c>, or empty.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int Add(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, PdfDictionary annotation, uint? color, string contents, ReadOnlySpan<byte> subject)
    {
        Finish(annotationState, annotation, color, contents, subject);
        return Changed(annotationState, pageIndex, PdfPageAnnotations.Append(annotationState.Store, page, annotation));
    }

    /// <summary>Sets the colour, opacity, flags, author, date, note text and subject of a new annotation.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour, or <see langword="null"/> for none.</param>
    /// <param name="contents">The note text, or empty.</param>
    /// <param name="subject">The <c>/Subj</c>, or empty.</param>
    internal static void Finish(HyperPdfAnnotations annotationState, PdfDictionary annotation, uint? color, string contents, ReadOnlySpan<byte> subject)
    {
        if (color is { } rgb)
        {
            PdfAnnotations.SetColor(annotation, KnownName.C, rgb);
        }

        if (!annotation.Get(KnownName.CA).IsNumber)
        {
            PdfAnnotations.SetOpacity(annotation, 1);
        }

        PdfAnnotations.SetFlags(annotation, PrintFlags);
        PdfAnnotations.SetText(annotation, KnownName.T, GetAuthor(annotationState));
        SetModified(annotationState, annotation);
        if (contents.Length > 0)
        {
            PdfAnnotations.SetText(annotation, KnownName.Contents, contents);
        }

        if (!subject.IsEmpty)
        {
            annotation.Set(annotationState.Names.Subject, PdfValue.FromString(subject.ToArray()));
        }
    }

    /// <summary>Records an edit when an index shows one was made.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="index">The new annotation index, or -1.</param>
    /// <returns>The index.</returns>
    internal static int Changed(HyperPdfAnnotations annotationState, int pageIndex, int index)
    {
        _ = Changed(annotationState, pageIndex, index >= 0);
        return index;
    }

    /// <summary>Records an edit when it succeeded, forgetting the page's cached annotations.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    internal static bool Changed(HyperPdfAnnotations annotationState, int pageIndex, bool changed)
    {
        if (changed)
        {
            annotationState.ForgetPage(pageIndex);
            _ = Interlocked.Increment(ref annotationState.UnsavedChanges);
        }

        return changed;
    }
}
