// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes embedded files meet PDF/A. PDF/A-1 allows none (ISO 19005-1 §6.1.11). PDF/A-2 and plain PDF/A-4 allow only
/// PDF/A files (ISO 19005-2 §6.8; ISO 19005-4:2020 §6.9), so other attachments stop the conversion rather than being
/// deleted. Every file specification gets /F and /UF, and from PDF/A-3 on a relationship, a MIME type, and a link from
/// the catalog's associated files (ISO 19005-3 §6.8). PDF/A-4f needs at least one attachment.
/// </summary>
internal static class PdfAConversionAttachments
{
    /// <summary>The name used when a file specification names no file.</summary>
    private const string UnnamedFile = "attachment";

    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var files = PdfAConversionAttachmentList.Find(session);
        if (files.Count == 0)
        {
            if (session.Level == PdfALevel.PdfA4F)
            {
                session.Refuse("PDF/A-4f is for documents with attached files, and this document has none. Choose PDF/A-4 instead.", PdfARule.EmbeddedFilesRequired);
            }

            return;
        }

        if (session.Part == PdfAClaim.Part1)
        {
            session.Refuse(
                "The document has attached files, and PDF/A-1 does not allow them. Choose PDF/A-3 to keep them.",
                PdfARule.EmbeddedFilesForbidden,
                PdfARule.FileSpecificationEmbeddedFileForbidden);
            return;
        }

        if (session.Level is PdfALevel.PdfA2B or PdfALevel.PdfA2U or PdfALevel.PdfA2A or PdfALevel.PdfA4)
        {
            PdfAConversionAttachmentCheck.RequirePdfA(session, files);
        }

        _ = PdfAConversionRewriter.Rewrite(session, 0, static (s, _, dictionary) => FixSpecification(s, dictionary));
        if (session.Part == PdfAClaim.Part3)
        {
            Associate(session, files);
        }

        session.Changed(
            "Gave each attached file a name, a relationship to the document and a file type.",
            PdfARule.FileSpecificationNames,
            PdfARule.FileSpecificationRelationshipRequired,
            PdfARule.EmbeddedFileMimeType);
    }

    /// <summary>Fixes a file specification that embeds a file.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>A fixed copy, or <see langword="null"/>.</returns>
    private static PdfDictionary? FixSpecification(PdfAConversionSession session, PdfDictionary dictionary)
    {
        if (!dictionary.ContainsKey(KnownName.EF))
        {
            return null;
        }

        var copy = dictionary.Clone();
        var name = dictionary.GetText(KnownName.UF) ?? dictionary.GetText(KnownName.F) ?? UnnamedFile;
        if (!dictionary.ContainsKey(KnownName.F))
        {
            copy.Set(KnownName.F, PdfValue.FromString(PdfText.Encode(name)));
        }

        if (!dictionary.ContainsKey(KnownName.UF))
        {
            copy.Set(KnownName.UF, PdfValue.FromString(PdfText.Encode(name)));
        }

        var relationship = session.Store.Names.Intern("AFRelationship"u8);
        if (session.Part >= PdfAClaim.Part3 && !dictionary.ContainsKey(relationship))
        {
            copy.Set(relationship, PdfValue.FromName(session.Store.Names.Intern("Unspecified"u8)));
        }

        if (session.Part >= PdfAClaim.Part3)
        {
            FixMimeTypes(session, dictionary.GetDictionary(KnownName.EF), name);
        }

        return copy.Count == dictionary.Count ? null : copy;
    }

    /// <summary>Gives each embedded file stream a MIME type when it has no valid one.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="embedded">The /EF dictionary, or <see langword="null"/>.</param>
    /// <param name="name">The file name, for guessing the type.</param>
    private static void FixMimeTypes(PdfAConversionSession session, PdfDictionary? embedded, string name)
    {
        var store = session.Store;
        for (var i = 0; embedded is not null && i < embedded.Count; i++)
        {
            var raw = embedded.GetValueAt(i);
            if (!raw.IsReference || StoreReading.Resolve(store, raw).AsStream() is not { } stream)
            {
                continue;
            }

            var subtype = store.Names.GetString(stream.Dictionary.GetName(KnownName.Subtype));
            if (PdfAConversionMimeTypes.IsValid(subtype))
            {
                continue;
            }

            var dictionary = stream.Dictionary.Clone();
            dictionary.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern(System.Text.Encoding.UTF8.GetBytes(PdfAConversionMimeTypes.Guess(name)))));
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromStream(new(dictionary, stream.CopyRawData())));
        }
    }

    /// <summary>Lists every indirect file specification in the catalog's /AF array, so each is an associated file.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="files">The file specifications found.</param>
    private static void Associate(PdfAConversionSession session, PdfAConversionAttachmentList files)
    {
        if (files.Direct > 0)
        {
            session.Refuse("An attached file is stored in a way PDF/A-3 cannot link to the document.", PdfARule.FileSpecificationAssociated);
            return;
        }

        var store = session.Store;
        var listed = new HashSet<int>();
        var graph = PdfObjectGraph.Collect(store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            CollectAssociated(store, graph.GetValue(number), listed);
        }

        var catalog = store.Catalog.Clone();
        var associated = StoreReading.Resolve(store, catalog.GetRaw(KnownName.AF)).AsArray()?.Clone() ?? new PdfArray(store, files.References.Count);
        var added = 0;
        foreach (var number in files.References)
        {
            if (!listed.Add(number))
            {
                continue;
            }

            associated.Add(PdfValue.FromReference(new(number, StoreEditing.GetGeneration(store, number))));
            added++;
        }

        if (added <= 0)
        {
            return;
        }

        catalog.Set(KnownName.AF, PdfValue.FromArray(associated));
        PdfAConversionRewriter.ReplaceCatalog(store, catalog);
        session.Changed(
            string.Create(CultureInfo.InvariantCulture, $"Linked {added} attached files to the document as associated files."),
            PdfARule.FileSpecificationAssociated);
    }

    /// <summary>Collects the file specifications an object's /AF arrays already list.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="value">The object.</param>
    /// <param name="listed">The object numbers listed.</param>
    private static void CollectAssociated(PdfObjectStore store, PdfValue value, HashSet<int> listed)
    {
        if (value.AsDictionary() is not { } dictionary || StoreReading.Resolve(store, dictionary.GetRaw(KnownName.AF)).AsArray() is not { } array)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array.GetRaw(i).IsReference)
            {
                _ = listed.Add(array.GetRaw(i).AsReference().Number);
            }
        }
    }
}
