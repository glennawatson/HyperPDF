// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Adds, changes and removes embedded files (ISO 32000-2 7.11.4). Attachments are named by the
/// <see cref="PdfAttachment.Key"/> that <see cref="PdfDocumentAttachments.GetAttachments"/> reports. Each edit joins the
/// open transaction or makes its own, so it can be undone. A document certified with a signature refuses the edit, and a
/// document that claims PDF/A-1 or PDF/A-2 refuses to take a file, as only PDF/A-3 and PDF/A-4F allow any file.
/// </summary>
public static class PdfDocumentAttachmentEditing
{
    /// <summary>What an edit of the attachments is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the attached files";

    /// <summary>Embeds a file. The bytes are compressed unless the spec says otherwise, and the stream records the size, dates and MD5 checksum.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The file.</param>
    /// <returns>The object of the new file specification.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty or already used, or the MIME type is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or claims a PDF/A level that does not allow the file.</exception>
    public static PdfObjectId AddAttachment(PdfDocument document, PdfAttachmentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(spec);
        PdfEditConformance.RequireEmbeddedFilesAllowed(document);
        var state = new AddState(spec);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Attach file",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            (edits, run) => run.Id = PdfAttachmentEditing.Add(edits, document, run.Spec, CancellationToken.None));
        return state.Id;
    }

    /// <summary>Embeds a file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The file.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The object of the new file specification.</returns>
    public static async ValueTask<PdfObjectId> AddAttachmentAsync(PdfDocument document, PdfAttachmentSpec spec, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return AddAttachment(document, spec);
    }

    /// <summary>Removes an embedded file. Its objects are deleted unless an annotation or an associated files list still uses the file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's <see cref="PdfAttachment.Key"/>.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static bool RemoveAttachment(PdfDocument document, string key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        var state = new KeyState(key, null);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Remove attached file",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            (edits, run) => run.Done = PdfAttachmentEditing.Remove(edits, document, run.Key, CancellationToken.None));
        return state.Done;
    }

    /// <summary>Removes an embedded file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's <see cref="PdfAttachment.Key"/>.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    public static async ValueTask<bool> RemoveAttachmentAsync(PdfDocument document, string key, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return RemoveAttachment(document, key);
    }

    /// <summary>Changes an embedded file's description, MIME type, relationship, dates, bytes or name. Changing the bytes works out the size and checksum again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's <see cref="PdfAttachment.Key"/>.</param>
    /// <param name="change">The changes.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The new name is empty or already used, or the MIME type is not valid.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or the attachment is stored inside the name tree.</exception>
    public static bool UpdateAttachment(PdfDocument document, string key, PdfAttachmentChange change)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(change);
        var state = new KeyState(key, change);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Change attached file",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            (edits, run) => run.Done = PdfAttachmentEditing.Update(edits, document, run.Key, run.Change!, CancellationToken.None));
        return state.Done;
    }

    /// <summary>Changes an embedded file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The attachment's <see cref="PdfAttachment.Key"/>.</param>
    /// <param name="change">The changes.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the document had the attachment.</returns>
    public static async ValueTask<bool> UpdateAttachmentAsync(PdfDocument document, string key, PdfAttachmentChange change, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return UpdateAttachment(document, key, change);
    }

    /// <summary>Loads the name tree an edit will read.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the nodes are cached.</returns>
    private static ValueTask PrefetchAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles) is { } root
            ? PdfPrefetcher.PrefetchAsync(document.Objects, root, PdfPrefetchKind.PageTree, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>The arguments of an add, and the file specification it made.</summary>
    /// <param name="spec">The file.</param>
    [DebuggerDisplay("AddState: {Spec}")]
    private sealed class AddState(PdfAttachmentSpec spec)
    {
        /// <summary>Gets the file.</summary>
        internal PdfAttachmentSpec Spec { get; } = spec;

        /// <summary>Gets or sets the file specification the add made.</summary>
        internal PdfObjectId Id { get; set; }
    }

    /// <summary>The arguments of a removal or change, and whether the attachment was there.</summary>
    /// <param name="key">The attachment's name in the name tree.</param>
    /// <param name="change">The changes, or <see langword="null"/> for a removal.</param>
    [DebuggerDisplay("KeyState: {Key}")]
    private sealed class KeyState(string key, PdfAttachmentChange? change)
    {
        /// <summary>Gets the attachment's name in the name tree.</summary>
        internal string Key { get; } = key;

        /// <summary>Gets the changes, or <see langword="null"/> for a removal.</summary>
        internal PdfAttachmentChange? Change { get; } = change;

        /// <summary>Gets or sets a value indicating whether the document had the attachment.</summary>
        internal bool Done { get; set; }
    }
}
