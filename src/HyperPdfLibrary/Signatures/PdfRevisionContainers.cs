// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Classifies catalog, page and interactive form changes.</summary>
internal static class PdfRevisionContainers
{
    /// <summary>Gets the spelling of the catalog's /Extensions key, which writers update with the /DSS store.</summary>
    internal static ReadOnlySpan<byte> ExtensionsKey => "Extensions"u8;

    /// <summary>Gets the spelling of a page's /Annots key.</summary>
    internal static ReadOnlySpan<byte> AnnotsKey => "Annots"u8;

    /// <summary>Gets the spelling of the interactive form's /Fields key.</summary>
    internal static ReadOnlySpan<byte> FieldsKey => "Fields"u8;

    /// <summary>Classifies a catalog change by the keys that changed.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The current catalog, or <see langword="null"/>.</param>
    /// <param name="before">The signed catalog, or <see langword="null"/>.</param>
    /// <returns>The kinds.</returns>
    internal static PdfModificationKinds ClassifyCatalog(in PdfRevisionComparison state, PdfDictionary? now, PdfDictionary? before)
    {
        if (now is null || before is null)
        {
            return PdfModificationKinds.Other;
        }

        var kinds = PdfModificationKinds.None;
        for (var i = 0; i < now.Count; i++)
        {
            var spelling = state.Tables.Left.GetSpelling(now.GetKeyAt(i));
            var old = before.GetRaw(state.Tables.Right.Intern(spelling));
            if (old.IsNull || !PdfValueComparer.Equal(now.GetValueAt(i), state.Tables.Left, old, state.Tables.Right))
            {
                kinds |= PdfRevisionContainers.CatalogKeyKind(state, spelling, now.Get(now.GetKeyAt(i)).AsDictionary(), before.Get(state.Tables.Right.Intern(spelling)).AsDictionary());
            }
        }

        for (var i = 0; i < before.Count; i++)
        {
            var spelling = state.Tables.Right.GetSpelling(before.GetKeyAt(i));
            if (!now.ContainsKey(state.Tables.Left.Intern(spelling)))
            {
                kinds |= PdfRevisionContainers.CatalogKeyKind(state, spelling, null, null);
            }
        }

        return kinds == PdfModificationKinds.None ? PdfModificationKinds.Other : kinds;
    }

    /// <summary>Gets the kind of change a catalog key makes.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="spelling">The key.</param>
    /// <param name="now">The key's current value as a dictionary, or <see langword="null"/>.</param>
    /// <param name="before">The key's signed value as a dictionary, or <see langword="null"/>.</param>
    /// <returns>The kind.</returns>
    internal static PdfModificationKinds CatalogKeyKind(in PdfRevisionComparison state, ReadOnlySpan<byte> spelling, PdfDictionary? now, PdfDictionary? before)
    {
        if (spelling.SequenceEqual(ExtensionsKey))
        {
            // Declaring the extension that validation data or a timestamp needs (ETSI EN 319 142-1, 5.6.1) adds an entry
            // and leaves every declared one alone, so it travels with the security store it belongs to.
            return PdfRevisionContainers.ExtensionsOnlyAdded(state, now, before) ? PdfModificationKinds.SecurityStore : PdfModificationKinds.Metadata;
        }

        _ = PdfNameTable.TryGetKnown(spelling, out var name);
        return name.ToKnownName() switch
        {
            KnownName.DSS => PdfModificationKinds.SecurityStore,
            KnownName.AcroForm => PdfRevisionContainers.ClassifyAcroForm(state, now, before),
            KnownName.Metadata or KnownName.Version => PdfModificationKinds.Metadata,
            KnownName.Pages => PdfModificationKinds.Pages,
            _ => PdfModificationKinds.Other,
        };
    }

    /// <summary>Determines whether a changed /Extensions dictionary only gained entries.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The current dictionary, or <see langword="null"/> when the entry was removed.</param>
    /// <param name="before">The signed dictionary, or <see langword="null"/> when there was none.</param>
    /// <returns><see langword="true"/> when every signed entry is still there with the same value.</returns>
    internal static bool ExtensionsOnlyAdded(in PdfRevisionComparison state, PdfDictionary? now, PdfDictionary? before)
    {
        if (before is null)
        {
            return now is not null;
        }

        if (now is null)
        {
            return false;
        }

        for (var i = 0; i < before.Count; i++)
        {
            var spelling = state.Tables.Right.GetSpelling(before.GetKeyAt(i));
            var current = now.GetRaw(state.Tables.Left.Intern(spelling));
            if (current.IsNull || !PdfValueComparer.Equal(current, state.Tables.Left, before.GetValueAt(i), state.Tables.Right))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies a page change: annotations added or removed, or a change to the page itself.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The current page.</param>
    /// <param name="before">The signed page.</param>
    /// <returns>The kinds.</returns>
    internal static PdfModificationKinds ClassifyPage(in PdfRevisionComparison state, PdfDictionary now, PdfDictionary before)
    {
        if (!PdfValueComparer.DictionaryEqual(state.Tables, now, before, AnnotsKey, 0))
        {
            return PdfModificationKinds.Pages;
        }

        var kinds = PdfRevisionContainers.ReferenceDifference(state, now.GetArray(KnownName.Annots), before.GetArray(KnownName.Annots));
        return kinds == PdfModificationKinds.None ? PdfModificationKinds.Annotation : kinds;
    }

    /// <summary>Classifies an interactive form change: fields added or removed, or other form settings.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The current form, or <see langword="null"/>.</param>
    /// <param name="before">The signed form, or <see langword="null"/>.</param>
    /// <returns>The kinds.</returns>
    internal static PdfModificationKinds ClassifyAcroForm(in PdfRevisionComparison state, PdfDictionary? now, PdfDictionary? before)
    {
        if (now is null || before is null)
        {
            return PdfModificationKinds.FormFill;
        }

        var kinds = PdfRevisionContainers.ReferenceDifference(state, now.GetArray(KnownName.Fields), before.GetArray(KnownName.Fields));
        if (!PdfValueComparer.DictionaryEqual(state.Tables, now, before, FieldsKey, 0))
        {
            // Signing sets /SigFlags; anything else here is form data such as /DR or /NeedAppearances.
            kinds |= ((kinds & PdfModificationKinds.Signature) == PdfModificationKinds.Signature) ? PdfModificationKinds.None : PdfModificationKinds.FormFill;
        }

        return kinds == PdfModificationKinds.None ? PdfModificationKinds.FormFill : kinds;
    }

    /// <summary>Classifies the objects referenced by one array and not the other.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The current array, or <see langword="null"/>.</param>
    /// <param name="before">The signed array, or <see langword="null"/>.</param>
    /// <returns>The kinds of the added and removed objects that are recognised.</returns>
    internal static PdfModificationKinds ReferenceDifference(in PdfRevisionComparison state, PdfArray? now, PdfArray? before)
    {
        var kinds = PdfModificationKinds.None;
        for (var i = 0; now is not null && i < now.Count; i++)
        {
            var item = now.GetRaw(i);
            if (item.IsReference && !PdfRevisionKinds.Contains(before, item.AsReference().Number))
            {
                kinds |= PdfRevisionKinds.KindOfTarget(StoreReading.GetObject(state.Current, item.AsReference()).AsDictionary(), state.Tables.Left);
            }
        }

        for (var i = 0; before is not null && i < before.Count; i++)
        {
            var item = before.GetRaw(i);
            if (item.IsReference && !PdfRevisionKinds.Contains(now, item.AsReference().Number))
            {
                kinds |= PdfRevisionKinds.KindOfTarget(StoreReading.GetObject(state.Signed, item.AsReference()).AsDictionary(), state.Tables.Right);
            }
        }

        return kinds;
    }
}
