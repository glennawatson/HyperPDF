// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds and removes file attachment annotations (ISO 32000-2 12.5.6.15): an icon on a page that holds an embedded file.</summary>
internal static class PdfFileAttachmentAnnotationWriter
{
    /// <summary>The annotation flag for printing (ISO 32000-2 12.5.3, Table 167).</summary>
    private const int PrintFlag = 4;

    /// <summary>The edge of the square the icon drawings use.</summary>
    private const int IconSize = 20;

    /// <summary>Gets what each icon draws in a 20 by 20 square.</summary>
    private static ReadOnlySpan<byte> PushPinDrawing => "1 0.85 0.2 rg 0.4 0.3 0 RG 1 w 6 12 8 6 re B 10 12 m 10 2 l S"u8;

    /// <summary>Gets the drawing of the chart icon.</summary>
    private static ReadOnlySpan<byte> GraphDrawing => "0.9 0.9 1 rg 0 0 0 RG 1 w 2 2 16 16 re B 0.2 0.4 0.8 rg 4 4 3 6 re f 9 4 3 10 re f 14 4 3 4 re f"u8;

    /// <summary>Gets the drawing of the paper clip icon.</summary>
    private static ReadOnlySpan<byte> PaperclipDrawing => "0.1 0.1 0.6 RG 1.5 w 1 J 1 j 6 3 m 6 15 l 10 18 l 14 15 l 14 5 l 10 3 l 10 12 l S"u8;

    /// <summary>Gets the drawing of the tag icon.</summary>
    private static ReadOnlySpan<byte> TagDrawing => "1 0.9 0.4 rg 0.4 0.3 0 RG 1 w 3 4 m 13 4 l 18 10 l 13 16 l 3 16 l h B"u8;

    /// <summary>Adds a file attachment annotation to a page, with the file it holds.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="rectangle">Where the icon goes, in default user space.</param>
    /// <param name="file">The file.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="contents">The annotation's text, which a screen reader announces; the file name when <see langword="null"/>.</param>
    /// <returns>The annotation's object.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is not a page of the document.</exception>
    /// <exception cref="InvalidOperationException">The page cannot be changed.</exception>
    internal static PdfObjectId Add(PdfObjectEdits edits, PdfDocument document, int pageIndex, PdfRectangle rectangle, PdfAttachmentSpec file, PdfAttachmentIcon icon, string? contents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var needsAssociation = PdfEditConformance.NeedsAssociatedFile(document);
        var fileId = PdfEmbeddedFileWriter.Create(edits, document, file, needsAssociation);
        var appearance = edits.Transaction.Add(PdfValue.FromStream(CreateAppearance(store, icon)));
        var annotation = new PdfDictionary(store);
        annotation.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        annotation.Set(KnownName.Subtype, PdfValue.FromName(KnownName.FileAttachment));
        annotation.Set(KnownName.Rect, PdfValue.FromArray(rectangle.ToArray(store)));
        annotation.Set(KnownName.P, PdfValue.FromReference(page.Id));
        annotation.Set(store.Names.Intern("FS"u8), PdfValue.FromReference(fileId));
        annotation.Set(KnownName.Name, PdfValue.FromName(store.Names.Intern(icon.ToString())));
        annotation.Set(KnownName.Contents, PdfValue.FromString(PdfText.Encode(contents ?? file.Name, store.Version)));
        annotation.Set(KnownName.F, PdfValue.FromInteger(PrintFlag));
        var states = new PdfDictionary(store);
        states.Set(KnownName.N, PdfValue.FromReference(appearance));
        annotation.Set(KnownName.AP, PdfValue.FromDictionary(states));
        if (needsAssociation)
        {
            annotation.Set(store.Names.Intern("AF"u8), PdfValue.FromArray(new(store, [PdfValue.FromReference(fileId)])));
        }

        var index = PdfPageAnnotations.Append(store, page, annotation);
        return index >= 0 ? PdfPageAnnotations.GetId(store, page, index) : throw new InvalidOperationException("The page's annotations cannot be changed.");
    }

    /// <summary>Removes a file attachment annotation, and the file it holds unless something else uses it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="id">The annotation's object.</param>
    /// <returns><see langword="true"/> when the page had the annotation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is not a page of the document.</exception>
    internal static bool Remove(PdfObjectEdits edits, PdfDocument document, int pageIndex, PdfObjectId id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var array = PdfPageAnnotations.GetArray(store, page);
        var index = IndexOf(array, id);
        var annotation = StoreReading.GetDictionary(store, id);
        if (index < 0 || annotation is null || !annotation.IsName(KnownName.Subtype, KnownName.FileAttachment) || !PdfPageAnnotations.RemoveAt(store, page, index))
        {
            return false;
        }

        var fileId = annotation.GetRaw(store.Names.Intern("FS"u8)).AsReference();
        var appearance = annotation.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N).AsReference() ?? default;
        edits.Delete(id);
        if (appearance.IsValid)
        {
            edits.Delete(appearance);
        }

        PdfAttachmentEditing.DeleteUnlessUsed(edits, document, fileId, string.Empty);
        return true;
    }

    /// <summary>Finds the position of an object in an annotation array.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="id">The object.</param>
    /// <returns>The position, or -1.</returns>
    private static int IndexOf(PdfArray? array, PdfObjectId id)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.GetRaw(i).AsReference().Number == id.Number)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Builds the appearance of an icon: a form drawn in a 20 by 20 square that the viewer scales to the annotation's rectangle.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="icon">The icon.</param>
    /// <returns>The form XObject.</returns>
    private static PdfStream CreateAppearance(PdfObjectStore store, PdfAttachmentIcon icon)
    {
        var form = new PdfDictionary(store);
        form.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        form.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        form.Set(KnownName.BBox, PdfValue.FromArray(PdfArray.FromNumbers(store, [0, 0, IconSize, IconSize])));
        form.Set(KnownName.Resources, PdfValue.FromDictionary(new(store)));
        return new(form, GetDrawing(icon).ToArray());
    }

    /// <summary>Gets the drawing of an icon.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The content stream bytes.</returns>
    private static ReadOnlySpan<byte> GetDrawing(PdfAttachmentIcon icon) => icon switch
    {
        PdfAttachmentIcon.Graph => GraphDrawing,
        PdfAttachmentIcon.Paperclip => PaperclipDrawing,
        PdfAttachmentIcon.Tag => TagDrawing,
        _ => PushPinDrawing,
    };
}
