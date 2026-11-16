// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Takes hidden information out of a document before it is shared: descriptive metadata, scripts, embedded files and comments. The change is one undo step.</summary>
public static class PdfDocumentSanitizer
{
    /// <summary>The descriptive metadata of a document with every text field cleared.</summary>
    private static readonly PdfMetadataEdit Cleared = new()
    {
        Title = string.Empty,
        Author = string.Empty,
        Subject = string.Empty,
        Keywords = string.Empty,
        Creator = string.Empty,
        Producer = string.Empty,
    };

    /// <summary>Takes the chosen things out of a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What to take out.</param>
    /// <returns>What was taken out.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfSanitizeReport Sanitize(PdfDocument document, PdfSanitizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var tally = new SanitizeTally();
        PdfDocumentEditing.RunEdit(
            document,
            "Remove hidden information",
            PdfChangeKinds.PageChanges | PdfChangeKinds.Metadata | PdfChangeKinds.Annotations,
            new SanitizeEdit(document, options, tally),
            Run);
        return new(tally.Scripts, tally.Attachments, tally.Comments, options.Metadata);
    }

    /// <summary>Tells whether an action runs a script.</summary>
    /// <param name="action">The action, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is a JavaScript action.</returns>
    private static bool IsScript(PdfDictionary? action) => action is not null && action.GetName(KnownName.S).Is(KnownName.JavaScript);

    /// <summary>Takes the actions that run scripts out of a dictionary: its action, and the ones among its triggers.</summary>
    /// <param name="owner">The catalog, page, link or form field.</param>
    /// <param name="tally">Counts what is removed.</param>
    private static void StripScripts(PdfDictionary owner, SanitizeTally tally)
    {
        if (IsScript(owner.GetDictionary(KnownName.A)) && owner.Remove(KnownName.A))
        {
            tally.Scripts++;
        }

        if (owner.GetDictionary(KnownName.AA) is not { } triggers)
        {
            return;
        }

        var scripts = new List<PdfName>();
        for (var i = 0; i < triggers.Count; i++)
        {
            if (IsScript(triggers.GetDictionary(triggers.GetKeyAt(i))))
            {
                scripts.Add(triggers.GetKeyAt(i));
            }
        }

        foreach (var key in scripts)
        {
            _ = triggers.Remove(key);
            tally.Scripts++;
        }

        if (triggers.Count == 0)
        {
            _ = owner.Remove(KnownName.AA);
        }
    }

    /// <summary>Does the whole pass inside the open transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The document, the options and the tally.</param>
    private static void Run(PdfEditTransaction transaction, SanitizeEdit edit)
    {
        var document = edit.Document;
        if (edit.Options.Attachments)
        {
            edit.Tally.Attachments += PdfDocumentAttachments.GetAttachments(document).Count;
        }

        if (edit.Options.Scripts)
        {
            edit.Tally.Scripts += PdfDocumentActions.GetDocumentScripts(document).Length;
        }

        SanitizeCatalog(transaction, edit);
        for (var index = 0; index < document.PageCount; index++)
        {
            SanitizePage(document, PdfDocumentPages.GetPage(document, index), edit);
        }

        if (edit.Options.Metadata)
        {
            PdfDocumentMetadataEditing.SetMetadata(document, Cleared);
        }
    }

    /// <summary>Takes what the options name out of the catalog: scripts, embedded files and application data.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The document, the options and the tally.</param>
    private static void SanitizeCatalog(PdfEditTransaction transaction, SanitizeEdit edit)
    {
        var document = edit.Document;
        var options = edit.Options;
        var catalog = document.Catalog.Clone();
        if (options.Scripts)
        {
            StripScripts(catalog, edit.Tally);
            if (IsScript(catalog.GetDictionary(KnownName.OpenAction)) && catalog.Remove(KnownName.OpenAction))
            {
                edit.Tally.Scripts++;
            }
        }

        StripNames(catalog, options);

        // PDF/A-4 keeps the modification date in /Info only when the catalog has /PieceInfo, so that conformance claim decides.
        if (options.Metadata && PdfDocumentMetadata.GetXmp(document)?.PdfAPart != PdfAClaim.Part4)
        {
            _ = catalog.Remove(KnownName.PieceInfo);
        }

        PdfPageTreeWriter.ReplaceCatalog(transaction, document.Objects, catalog);
    }

    /// <summary>Takes the script and embedded file name trees out of the catalog's names.</summary>
    /// <param name="catalog">The copy of the catalog being changed.</param>
    /// <param name="options">What to take out.</param>
    private static void StripNames(PdfDictionary catalog, PdfSanitizeOptions options)
    {
        if (catalog.GetDictionary(KnownName.Names)?.Clone() is { } names)
        {
            _ = options.Scripts && names.Remove(KnownName.JavaScript);
            _ = options.Attachments && names.Remove(KnownName.EmbeddedFiles);
            catalog.Set(KnownName.Names, PdfValue.FromDictionary(names));
        }

        if (options.Attachments)
        {
            _ = catalog.Remove(KnownName.AF);
        }
    }

    /// <summary>Takes what the options name out of a page and its notes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="edit">The document, the options and the tally.</param>
    private static void SanitizePage(PdfDocument document, PdfPage page, SanitizeEdit edit)
    {
        var store = document.Objects;
        var dictionary = PdfPageAnnotations.GetPageDictionary(store, page);
        if (edit.Options.Scripts)
        {
            StripScripts(dictionary, edit.Tally);
        }

        if (edit.Options.Metadata)
        {
            _ = dictionary.Remove(KnownName.PieceInfo);
        }

        if (PdfPageAnnotations.GetArray(store, page) is not { } notes)
        {
            return;
        }

        for (var i = notes.Count - 1; i >= 0; i--)
        {
            if (notes.GetDictionary(i) is { } note)
            {
                SanitizeNote(store, page, i, note, edit);
            }
        }
    }

    /// <summary>Removes a note when the options ask for it, or takes the scripts out of it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The note's place on the page.</param>
    /// <param name="note">The note.</param>
    /// <param name="edit">The document, the options and the tally.</param>
    private static void SanitizeNote(PdfObjectStore store, PdfPage page, int index, PdfDictionary note, SanitizeEdit edit)
    {
        var subtype = note.GetName(KnownName.Subtype);
        if (edit.Options.Attachments && subtype.Is(KnownName.FileAttachment) && PdfPageAnnotations.RemoveAt(store, page, index))
        {
            edit.Tally.Attachments++;
            return;
        }

        var markup = !subtype.Is(KnownName.Link) && !subtype.Is(KnownName.Widget);
        if (edit.Options.Comments && markup && PdfPageAnnotations.RemoveAt(store, page, index))
        {
            edit.Tally.Comments++;
            return;
        }

        if (edit.Options.Scripts)
        {
            StripScripts(note, edit.Tally);
        }
    }

    /// <summary>The state of one sanitising pass.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Options">What to take out.</param>
    /// <param name="Tally">Counts what is taken out.</param>
    private sealed record SanitizeEdit(PdfDocument Document, PdfSanitizeOptions Options, SanitizeTally Tally);
}
