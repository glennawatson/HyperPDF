// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Lists, adds and removes file attachment annotations (ISO 32000-2 12.5.6.15): icons on a page that each hold an
/// embedded file. Adding and removing is an annotation change, which a document certified for annotating (DocMDP level 3)
/// still allows. A document that claims PDF/UA refuses a new annotation, as it would need a place in the structure tree.
/// </summary>
public static class PdfDocumentFileAttachmentAnnotations
{
    /// <summary>What an edit of the annotations is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the annotations";

    /// <summary>Lists the file attachment annotations of every page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The annotations, page by page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PdfFileAttachmentAnnotation> GetFileAttachmentAnnotations(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new List<PdfFileAttachmentAnnotation>();
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            ReadPage(document, pageIndex, result);
        }

        return result;
    }

    /// <summary>Lists the file attachment annotations of one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The annotations, in the order the page lists them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page is not a page of the document.</exception>
    public static IReadOnlyList<PdfFileAttachmentAnnotation> GetFileAttachmentAnnotations(PdfDocument document, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        var result = new List<PdfFileAttachmentAnnotation>();
        ReadPage(document, pageIndex, result);
        return result;
    }

    /// <summary>Adds a file attachment annotation to a page, with the file it holds. The file is not listed in the document's attachments.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="rectangle">Where the icon goes, in default user space.</param>
    /// <param name="file">The file.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="contents">The annotation's text, which a screen reader announces; the file name when <see langword="null"/>.</param>
    /// <returns>The annotation's object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page is not a page of the document.</exception>
    /// <exception cref="ArgumentException">The file's name is empty or its MIME type is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, claims PDF/UA, or claims a PDF/A level that does not allow the file.</exception>
    public static PdfObjectId AddFileAttachmentAnnotation(PdfDocument document, int pageIndex, PdfRectangle rectangle, PdfAttachmentSpec file, PdfAttachmentIcon icon, string? contents)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(file);
        PdfEditConformance.RequireEmbeddedFilesAllowed(document);
        PdfEditConformance.RequireUntaggedAnnotationsAllowed(document);
        var state = new AddState(pageIndex, rectangle, file, icon, contents);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Attach file to page",
            PdfChangeKinds.Annotations,
            PdfCacheScopes.None,
            EditDescription,
            state,
            (edits, run) => run.Id = PdfFileAttachmentAnnotationWriter.Add(edits, document, run.PageIndex, run.Rectangle, run.File, run.Icon, run.Contents));
        return state.Id;
    }

    /// <summary>Removes a file attachment annotation. The file it holds is deleted unless the document's attachments or another annotation still use it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="id">The annotation's object, from <see cref="PdfFileAttachmentAnnotation.Id"/>.</param>
    /// <returns><see langword="true"/> when the page had the annotation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page is not a page of the document.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static bool RemoveFileAttachmentAnnotation(PdfDocument document, int pageIndex, PdfObjectId id)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new RemoveState(pageIndex, id);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Remove file attachment",
            PdfChangeKinds.Annotations,
            PdfCacheScopes.None,
            EditDescription,
            state,
            (edits, run) => run.Done = PdfFileAttachmentAnnotationWriter.Remove(edits, document, run.PageIndex, run.Id));
        return state.Done;
    }

    /// <summary>Reads the file attachment annotations of a page into a list.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="result">Receives the annotations.</param>
    private static void ReadPage(PdfDocument document, int pageIndex, List<PdfFileAttachmentAnnotation> result)
    {
        var fileSpec = document.Objects.Names.Intern("FS"u8);
        var annotations = PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && annotation.IsName(KnownName.Subtype, KnownName.FileAttachment))
            {
                result.Add(Read(document, pageIndex, annotations.GetRaw(i).AsReference(), annotation, fileSpec));
            }
        }
    }

    /// <summary>Reads one file attachment annotation of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The index of the page the annotation is on.</param>
    /// <param name="id">The annotation's object, or an invalid id when it is inline.</param>
    /// <param name="annotation">The annotation dictionary.</param>
    /// <param name="fileSpec">The <c>/FS</c> key.</param>
    /// <returns>The annotation as it is read.</returns>
    private static PdfFileAttachmentAnnotation Read(PdfDocument document, int pageIndex, PdfObjectId id, PdfDictionary annotation, PdfName fileSpec)
    {
        var icon = annotation.GetName(KnownName.Name);
        var files = annotation.Get(fileSpec).AsDictionary()?.GetDictionary(KnownName.EF);
        return new(
            pageIndex,
            id,
            PdfDocumentFileSpecs.ReadFileSpec(document, annotation.Get(fileSpec)),
            icon.IsNone ? nameof(PdfAttachmentIcon.PushPin) : document.Objects.Names.GetString(icon),
            annotation.GetText(KnownName.Contents),
            annotation.GetRaw(fileSpec).AsReference())
        {
            Bounds = annotation.TryGetRectangle(KnownName.Rect, out var rect) ? PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top) : default,
            Data = files?.GetStream(KnownName.UF) ?? files?.GetStream(KnownName.F),
        };
    }

    /// <summary>The arguments of an add, and the annotation it made.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="rectangle">Where the icon goes.</param>
    /// <param name="file">The file.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="contents">The annotation's text.</param>
    [DebuggerDisplay("AddState: page {PageIndex}")]
    private sealed class AddState(int pageIndex, PdfRectangle rectangle, PdfAttachmentSpec file, PdfAttachmentIcon icon, string? contents)
    {
        /// <summary>Gets the page.</summary>
        internal int PageIndex { get; } = pageIndex;

        /// <summary>Gets where the icon goes.</summary>
        internal PdfRectangle Rectangle { get; } = rectangle;

        /// <summary>Gets the file.</summary>
        internal PdfAttachmentSpec File { get; } = file;

        /// <summary>Gets the icon.</summary>
        internal PdfAttachmentIcon Icon { get; } = icon;

        /// <summary>Gets the annotation's text.</summary>
        internal string? Contents { get; } = contents;

        /// <summary>Gets or sets the annotation the add made.</summary>
        internal PdfObjectId Id { get; set; }
    }

    /// <summary>The arguments of a removal, and whether it removed the annotation.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="id">The annotation.</param>
    [DebuggerDisplay("RemoveState: page {PageIndex}")]
    private sealed class RemoveState(int pageIndex, PdfObjectId id)
    {
        /// <summary>Gets the page.</summary>
        internal int PageIndex { get; } = pageIndex;

        /// <summary>Gets the annotation.</summary>
        internal PdfObjectId Id { get; } = id;

        /// <summary>Gets or sets a value indicating whether the page had the annotation.</summary>
        internal bool Done { get; set; }
    }
}
