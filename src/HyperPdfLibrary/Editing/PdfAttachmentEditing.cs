// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds, changes and removes embedded files in the <c>/EmbeddedFiles</c> name tree.</summary>
internal static class PdfAttachmentEditing
{
    /// <summary>Embeds a file and lists it in the name tree, and in the catalog's <c>/AF</c> when the PDF/A level asks for it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="spec">The file.</param>
    /// <param name="cancellationToken">Cancels a rebuild of the name tree.</param>
    /// <returns>The file specification's object.</returns>
    /// <exception cref="ArgumentException">The name is empty or already used, or the MIME type is not valid.</exception>
    internal static PdfObjectId Add(PdfObjectEdits edits, PdfDocument document, PdfAttachmentSpec spec, CancellationToken cancellationToken)
    {
        var key = PdfValue.FromString(PdfText.Encode(spec.Name, document.Objects.Version));
        if (Find(document, spec.Name) is not null)
        {
            throw new ArgumentException($"The document already has an attached file named '{spec.Name}'.", nameof(spec));
        }

        var needsAssociation = PdfEditConformance.NeedsAssociatedFile(document);
        var id = PdfEmbeddedFileWriter.Create(edits, document, spec, needsAssociation);
        PdfNameTreeEditing.Set(edits, PdfDocumentNameTrees.GetKey(document.Objects, PdfNameTreeKind.EmbeddedFiles), key, PdfValue.FromReference(id), cancellationToken);
        if (needsAssociation)
        {
            AddAssociation(edits, id);
        }

        return id;
    }

    /// <summary>Removes a file from the name tree, and deletes it unless something else still uses it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's name in the name tree.</param>
    /// <param name="cancellationToken">Cancels a rebuild of the name tree.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    internal static bool Remove(PdfObjectEdits edits, PdfDocument document, string key, CancellationToken cancellationToken)
    {
        if (Find(document, key) is not { } attachment)
        {
            return false;
        }

        var tree = PdfDocumentNameTrees.GetKey(document.Objects, PdfNameTreeKind.EmbeddedFiles);
        _ = PdfNameTreeEditing.Remove(edits, tree, PdfValue.FromString(PdfText.Encode(key, document.Objects.Version)), cancellationToken);
        if (attachment.Id.IsValid)
        {
            RemoveAssociation(edits, attachment.Id);
            DeleteUnlessUsed(edits, document, attachment.Id, attachment.Key);
        }

        return true;
    }

    /// <summary>Changes an embedded file.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's name in the name tree.</param>
    /// <param name="change">The changes.</param>
    /// <param name="cancellationToken">Cancels a rebuild of the name tree.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    /// <exception cref="ArgumentException">The new name is empty or already used, or the MIME type is not valid.</exception>
    /// <exception cref="InvalidOperationException">The attachment's file specification is not an object of its own, so it cannot be changed in place.</exception>
    internal static bool Update(PdfObjectEdits edits, PdfDocument document, string key, PdfAttachmentChange change, CancellationToken cancellationToken)
    {
        if (Find(document, key) is not { } attachment)
        {
            return false;
        }

        if (!attachment.Id.IsValid)
        {
            throw new InvalidOperationException("The attached file is stored inside the name tree and cannot be changed. Remove it and attach it again.");
        }

        var store = document.Objects;
        var filespec = edits.Edit(attachment.Id)!;
        PdfEmbeddedFileWriter.SetDescription(filespec, change.Description, store.Version);
        PdfEmbeddedFileWriter.SetRelationship(store, filespec, change.Relationship);
        UpdateStream(edits, document, attachment, change);
        if (change.Name is { } name && !string.Equals(name, key, StringComparison.Ordinal))
        {
            Rename(edits, document, attachment, name, cancellationToken);
        }

        return true;
    }

    /// <summary>Deletes a file specification and its stream when no other name, list or annotation points at it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="id">The file specification.</param>
    /// <param name="removedKey">The name tree entry that was removed and does not count as a use; empty for none.</param>
    internal static void DeleteUnlessUsed(PdfObjectEdits edits, PdfDocument document, PdfObjectId id, string removedKey)
    {
        if (!id.IsValid || PdfFileReferences.IsUsed(document, id, removedKey))
        {
            return;
        }

        var streamId = GetStreamId(StoreReading.GetDictionary(document.Objects, id));
        var files = StoreReading.GetDictionary(document.Objects, id)?.GetDictionary(KnownName.EF);
        var other = files?.GetRaw(KnownName.F).AsReference() ?? default;
        edits.Delete(id);
        if (streamId.IsValid)
        {
            edits.Delete(streamId);
        }

        if (other.IsValid && other.Number != streamId.Number)
        {
            edits.Delete(other);
        }
    }

    /// <summary>Finds an attachment by its name in the name tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The name.</param>
    /// <returns>The attachment, or <see langword="null"/>.</returns>
    private static PdfAttachment? Find(PdfDocument document, string key)
    {
        foreach (var attachment in PdfDocumentAttachments.GetAttachments(document))
        {
            if (string.Equals(attachment.Key, key, StringComparison.Ordinal))
            {
                return attachment;
            }
        }

        return null;
    }

    /// <summary>Changes the MIME type, dates and bytes of the embedded file stream, when the change touches them.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="change">The changes.</param>
    private static void UpdateStream(PdfObjectEdits edits, PdfDocument document, PdfAttachment attachment, PdfAttachmentChange change)
    {
        var streamId = GetStreamId(edits.Peek(attachment.Id));
        if (!TouchesStream(change) || attachment.Data is not { } current || !streamId.IsValid)
        {
            return;
        }

        var bytes = change.Data is { } data ? data.ToArray() : current.DecodeToArray();
        var modified = change.Modified ?? (change.Data is null ? attachment.Modified : TimeProvider.System.GetUtcNow());
        var mime = change.MimeType ?? attachment.MimeType ?? string.Empty;
        var stream = PdfEmbeddedFileWriter.CreateStream(document.Objects, bytes, mime, change.Created ?? attachment.Created, modified, true);
        edits.Transaction.Replace(streamId, PdfValue.FromStream(stream));
    }

    /// <summary>Determines whether a change alters the embedded file stream rather than only the file specification.</summary>
    /// <param name="change">The change.</param>
    /// <returns><see langword="true"/> when it sets the MIME type, a date or the bytes.</returns>
    private static bool TouchesStream(PdfAttachmentChange change) => change.MimeType is not null || change.Created is not null || change.Modified is not null || change.Data is not null;

    /// <summary>Lists an attachment under a new name.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels a rebuild of the name tree.</param>
    /// <exception cref="ArgumentException">The name is empty or already used.</exception>
    private static void Rename(PdfObjectEdits edits, PdfDocument document, PdfAttachment attachment, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || Find(document, name) is not null)
        {
            throw new ArgumentException($"'{name}' is empty or already the name of another attached file.", nameof(name));
        }

        var store = document.Objects;
        var tree = PdfDocumentNameTrees.GetKey(store, PdfNameTreeKind.EmbeddedFiles);
        _ = PdfNameTreeEditing.Remove(edits, tree, PdfValue.FromString(PdfText.Encode(attachment.Key, store.Version)), cancellationToken);
        PdfNameTreeEditing.Set(edits, tree, PdfValue.FromString(PdfText.Encode(name, store.Version)), PdfValue.FromReference(attachment.Id), cancellationToken);
        var filespec = edits.Edit(attachment.Id)!;
        filespec.Set(KnownName.F, PdfValue.FromString(Encoding.ASCII.GetBytes(PdfEmbeddedFileWriter.AsciiName(name))));
        filespec.Set(KnownName.UF, PdfValue.FromString(PdfText.Encode(name, store.Version)));
    }

    /// <summary>Gets the object of the embedded file stream a file specification points at.</summary>
    /// <param name="filespec">The file specification, or <see langword="null"/>.</param>
    /// <returns>The stream's object, or an invalid id.</returns>
    private static PdfObjectId GetStreamId(PdfDictionary? filespec)
    {
        var files = filespec?.GetDictionary(KnownName.EF);
        var id = files?.GetRaw(KnownName.UF).AsReference() ?? default;
        return id.IsValid ? id : files?.GetRaw(KnownName.F).AsReference() ?? default;
    }

    /// <summary>Lists a file specification in the catalog's <c>/AF</c> array (ISO 32000-2 14.13.2).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="id">The file specification.</param>
    private static void AddAssociation(PdfObjectEdits edits, PdfObjectId id)
    {
        var catalog = edits.Catalog();
        var key = edits.Store.Names.Intern("AF"u8);
        var list = catalog.GetArray(key)?.Clone() ?? new PdfArray(edits.Store);
        list.Add(PdfValue.FromReference(id));
        catalog.Set(key, PdfValue.FromArray(list));
    }

    /// <summary>Takes a file specification out of the catalog's <c>/AF</c> array.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="id">The file specification.</param>
    private static void RemoveAssociation(PdfObjectEdits edits, PdfObjectId id)
    {
        var key = edits.Store.Names.Intern("AF"u8);
        if (edits.CatalogForRead.GetArray(key) is not { } list)
        {
            return;
        }

        var kept = new PdfArray(edits.Store, list.Count);
        foreach (var item in list.Items)
        {
            if (!(item.IsReference && item.AsReference().Number == id.Number))
            {
                kept.Add(item);
            }
        }

        if (kept.Count == list.Count)
        {
            return;
        }

        var catalog = edits.Catalog();
        if (kept.Count == 0)
        {
            _ = catalog.Remove(key);
        }
        else
        {
            catalog.Set(key, PdfValue.FromArray(kept));
        }
    }
}
