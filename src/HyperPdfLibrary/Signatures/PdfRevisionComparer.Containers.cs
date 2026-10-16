// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <content>Catalog, page and form changes, and the objects a change owns.</content>
internal sealed partial class PdfRevisionComparer
{
    /// <summary>Gets the spelling of the catalog's /Extensions key, which writers update with the /DSS store.</summary>
    private static ReadOnlySpan<byte> ExtensionsKey => "Extensions"u8;

    /// <summary>Gets the spelling of a page's /Annots key.</summary>
    private static ReadOnlySpan<byte> AnnotsKey => "Annots"u8;

    /// <summary>Gets the spelling of the interactive form's /Fields key.</summary>
    private static ReadOnlySpan<byte> FieldsKey => "Fields"u8;

    /// <summary>Classifies a catalog change by the keys that changed.</summary>
    /// <param name="now">The current catalog, or <see langword="null"/>.</param>
    /// <param name="before">The signed catalog, or <see langword="null"/>.</param>
    /// <returns>The kinds.</returns>
    private PdfModificationKinds ClassifyCatalog(PdfDictionary? now, PdfDictionary? before)
    {
        if (now is null || before is null)
        {
            return PdfModificationKinds.Other;
        }

        var kinds = PdfModificationKinds.None;
        for (var i = 0; i < now.Count; i++)
        {
            var spelling = _tables.Left.GetSpelling(now.GetKeyAt(i));
            var old = before.GetRaw(_tables.Right.Intern(spelling));
            if (old.IsNull || !PdfValueComparer.Equal(now.GetValueAt(i), _tables.Left, old, _tables.Right))
            {
                kinds |= CatalogKeyKind(spelling, now.Get(now.GetKeyAt(i)).AsDictionary(), before.Get(_tables.Right.Intern(spelling)).AsDictionary());
            }
        }

        for (var i = 0; i < before.Count; i++)
        {
            var spelling = _tables.Right.GetSpelling(before.GetKeyAt(i));
            if (!now.ContainsKey(_tables.Left.Intern(spelling)))
            {
                kinds |= CatalogKeyKind(spelling, null, null);
            }
        }

        return kinds == PdfModificationKinds.None ? PdfModificationKinds.Other : kinds;
    }

    /// <summary>Gets the kind of change a catalog key makes.</summary>
    /// <param name="spelling">The key.</param>
    /// <param name="now">The key's current value as a dictionary, or <see langword="null"/>.</param>
    /// <param name="before">The key's signed value as a dictionary, or <see langword="null"/>.</param>
    /// <returns>The kind.</returns>
    private PdfModificationKinds CatalogKeyKind(ReadOnlySpan<byte> spelling, PdfDictionary? now, PdfDictionary? before)
    {
        if (spelling.SequenceEqual(ExtensionsKey))
        {
            // Declaring the extension that validation data or a timestamp needs (ETSI EN 319 142-1, 5.6.1) adds an entry
            // and leaves every declared one alone, so it travels with the security store it belongs to.
            return ExtensionsOnlyAdded(now, before) ? PdfModificationKinds.SecurityStore : PdfModificationKinds.Metadata;
        }

        _ = PdfNameTable.TryGetKnown(spelling, out var name);
        return name.ToKnownName() switch
        {
            KnownName.DSS => PdfModificationKinds.SecurityStore,
            KnownName.AcroForm => ClassifyAcroForm(now, before),
            KnownName.Metadata or KnownName.Version => PdfModificationKinds.Metadata,
            KnownName.Pages => PdfModificationKinds.Pages,
            _ => PdfModificationKinds.Other,
        };
    }

    /// <summary>Determines whether a changed /Extensions dictionary only gained entries.</summary>
    /// <param name="now">The current dictionary, or <see langword="null"/> when the entry was removed.</param>
    /// <param name="before">The signed dictionary, or <see langword="null"/> when there was none.</param>
    /// <returns><see langword="true"/> when every signed entry is still there with the same value.</returns>
    private bool ExtensionsOnlyAdded(PdfDictionary? now, PdfDictionary? before)
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
            var spelling = _tables.Right.GetSpelling(before.GetKeyAt(i));
            var current = now.GetRaw(_tables.Left.Intern(spelling));
            if (current.IsNull || !PdfValueComparer.Equal(current, _tables.Left, before.GetValueAt(i), _tables.Right))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies a page change: annotations added or removed, or a change to the page itself.</summary>
    /// <param name="now">The current page.</param>
    /// <param name="before">The signed page.</param>
    /// <returns>The kinds.</returns>
    private PdfModificationKinds ClassifyPage(PdfDictionary now, PdfDictionary before)
    {
        if (!PdfValueComparer.DictionaryEqual(_tables, now, before, AnnotsKey, 0))
        {
            return PdfModificationKinds.Pages;
        }

        var kinds = ReferenceDifference(now.GetArray(KnownName.Annots), before.GetArray(KnownName.Annots));
        return kinds == PdfModificationKinds.None ? PdfModificationKinds.Annotation : kinds;
    }

    /// <summary>Classifies an interactive form change: fields added or removed, or other form settings.</summary>
    /// <param name="now">The current form, or <see langword="null"/>.</param>
    /// <param name="before">The signed form, or <see langword="null"/>.</param>
    /// <returns>The kinds.</returns>
    private PdfModificationKinds ClassifyAcroForm(PdfDictionary? now, PdfDictionary? before)
    {
        if (now is null || before is null)
        {
            return PdfModificationKinds.FormFill;
        }

        var kinds = ReferenceDifference(now.GetArray(KnownName.Fields), before.GetArray(KnownName.Fields));
        if (!PdfValueComparer.DictionaryEqual(_tables, now, before, FieldsKey, 0))
        {
            // Signing sets /SigFlags; anything else here is form data such as /DR or /NeedAppearances.
            kinds |= ((kinds & PdfModificationKinds.Signature) == PdfModificationKinds.Signature) ? PdfModificationKinds.None : PdfModificationKinds.FormFill;
        }

        return kinds == PdfModificationKinds.None ? PdfModificationKinds.FormFill : kinds;
    }

    /// <summary>Classifies the objects referenced by one array and not the other.</summary>
    /// <param name="now">The current array, or <see langword="null"/>.</param>
    /// <param name="before">The signed array, or <see langword="null"/>.</param>
    /// <returns>The kinds of the added and removed objects that are recognised.</returns>
    private PdfModificationKinds ReferenceDifference(PdfArray? now, PdfArray? before)
    {
        var kinds = PdfModificationKinds.None;
        for (var i = 0; now is not null && i < now.Count; i++)
        {
            var item = now.GetRaw(i);
            if (item.IsReference && !Contains(before, item.AsReference().Number))
            {
                kinds |= KindOfTarget(StoreReading.GetObject(_current, item.AsReference()).AsDictionary(), _tables.Left);
            }
        }

        for (var i = 0; before is not null && i < before.Count; i++)
        {
            var item = before.GetRaw(i);
            if (item.IsReference && !Contains(now, item.AsReference().Number))
            {
                kinds |= KindOfTarget(StoreReading.GetObject(_signed, item.AsReference()).AsDictionary(), _tables.Right);
            }
        }

        return kinds;
    }

    /// <summary>Marks the objects that serve a change: an annotation's appearances and icons, or the form's resources.</summary>
    /// <param name="change">The change.</param>
    /// <param name="now">The changed object's current value.</param>
    private void OwnParts(PdfObjectChange change, PdfValue now)
    {
        if (now.AsDictionary() is not { } dictionary)
        {
            return;
        }

        if (change.ObjectNumber == _acroForm)
        {
            Own(dictionary.GetRaw(KnownName.DR), change);
            return;
        }

        Own(dictionary.GetRaw(KnownName.AP), change);
        Own(dictionary.GetRaw(KnownName.MK), change);
    }

    /// <summary>Marks every object reachable from a value as serving a change, unless already marked.</summary>
    /// <param name="start">The value.</param>
    /// <param name="change">The change.</param>
    private void Own(PdfValue start, PdfObjectChange change)
    {
        if (start.IsNull)
        {
            return;
        }

        var owned = _owned ??= [];
        var pending = new Stack<PdfValue>();
        pending.Push(start);
        while (pending.Count > 0 && owned.Count < MaxOwnedObjects)
        {
            var value = pending.Pop();
            if (value.IsReference)
            {
                if (owned.TryAdd(value.AsReference().Number, change))
                {
                    pending.Push(StoreReading.GetObject(_current, value.AsReference()));
                }

                continue;
            }

            PushChildren(pending, value);
        }
    }
}
