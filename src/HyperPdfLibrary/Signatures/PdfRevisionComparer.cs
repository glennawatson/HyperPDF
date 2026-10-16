// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Finds the objects later revisions added, changed or removed after a signed revision, and classifies each change.
/// An object is a candidate when its cross-reference selection differs or its definition lies after the signed bytes;
/// it is a change when its value differs from the signed revision's. Objects that only serve another change, such as
/// an annotation's appearance stream or the /DSS store's streams, take that change's kind.
/// </summary>
[DebuggerDisplay("PdfRevisionComparer")]
internal sealed partial class PdfRevisionComparer
{
    /// <summary>The most objects walked to find what a change owns.</summary>
    private const int MaxOwnedObjects = 1 << 16;

    /// <summary>The document as it is now.</summary>
    private readonly PdfObjectStore _current;

    /// <summary>The document as it was signed.</summary>
    private readonly PdfObjectStore _signed;

    /// <summary>The two revisions' name tables.</summary>
    private readonly NameTables _tables;

    /// <summary>The catalog's object number, or -1.</summary>
    private readonly int _root;

    /// <summary>The information dictionary's object number, or -1.</summary>
    private readonly int _info;

    /// <summary>The interactive form dictionary's object number, or -1.</summary>
    private readonly int _acroForm;

    /// <summary>Objects that serve another change, with that change.</summary>
    private Dictionary<int, PdfObjectChange>? _owned;

    /// <summary>Initializes a new instance of the <see cref="PdfRevisionComparer"/> class.</summary>
    /// <param name="current">The document as it is now.</param>
    /// <param name="signed">The document as it was signed.</param>
    private PdfRevisionComparer(PdfObjectStore current, PdfObjectStore signed)
    {
        _current = current;
        _signed = signed;
        _tables = new(current.Names, signed.Names);
        _root = ReferenceNumber(current.Trailer.GetRaw(KnownName.Root));
        _info = ReferenceNumber(current.Trailer.GetRaw(KnownName.Info));
        _acroForm = ReferenceNumber(current.Catalog.GetRaw(KnownName.AcroForm));
        Own(current.Catalog.GetRaw(KnownName.DSS), new(0, PdfModificationKinds.SecurityStore, null, false));
    }

    /// <summary>Lists the changes made after the signed revision.</summary>
    /// <param name="current">The document as it is now.</param>
    /// <param name="signed">The document as it was signed.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <returns>The changes, by object number; none is marked permitted yet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static List<PdfObjectChange> Compare(PdfObjectStore current, PdfObjectStore signed, long signedEnd) => new PdfRevisionComparer(current, signed).Run(signedEnd);

    /// <summary>Gets the object number of a reference.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or -1 when the value is not a reference.</returns>
    private static int ReferenceNumber(PdfValue value) => value.IsReference ? value.AsReference().Number : -1;

    /// <summary>Classifies a changed stream: file structure, metadata, or not recognised.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    private static PdfObjectChange? ClassifyStream(int number, PdfStream stream)
    {
        var type = stream.Dictionary.GetName(KnownName.Type);
        if (type.Is(KnownName.XRef) || type.Is(KnownName.ObjStm))
        {
            return new(number, PdfModificationKinds.None, null, false);
        }

        return type.Is(KnownName.Metadata) ? new(number, PdfModificationKinds.Metadata, null, false) : null;
    }

    /// <summary>Compares every candidate object.</summary>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <returns>The changes.</returns>
    private List<PdfObjectChange> Run(long signedEnd)
    {
        var changes = new List<PdfObjectChange>();
        List<int>? unresolved = null;
        for (var number = 1; number < _current.XrefSize; number++)
        {
            if (!TryGetCandidate(number, signedEnd, out var now, out var before) || (!now.IsNull && !before.IsNull && PdfValueComparer.Equal(now, _tables.Left, before, _tables.Right)))
            {
                continue;
            }

            if (Classify(number, now, before) is not { } change)
            {
                (unresolved ??= []).Add(number);
            }
            else if (change.Kind != PdfModificationKinds.None)
            {
                changes.Add(change);
                OwnParts(change, now);
            }
        }

        CompleteUnresolved(unresolved, changes);
        changes.Sort(static (left, right) => left.ObjectNumber.CompareTo(right.ObjectNumber));
        return changes;
    }

    /// <summary>Classifies deferred objects after all their potential owners have been visited.</summary>
    /// <param name="unresolved">Deferred object numbers, or null when every candidate was classified.</param>
    /// <param name="changes">The classified changes.</param>
    private void CompleteUnresolved(List<int>? unresolved, List<PdfObjectChange> changes)
    {
        if (unresolved is null)
        {
            return;
        }

        foreach (var number in unresolved)
        {
            changes.Add(TryGetOwner(number, out var owner) ? owner with { ObjectNumber = number } : new(number, PdfModificationKinds.Other, null, false));
        }
    }

    /// <summary>Reads a change's ownership only when a reachable-object walk created the index.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="owner">The change that owns the object.</param>
    /// <returns>Whether an owner is recorded.</returns>
    private bool TryGetOwner(int number, out PdfObjectChange owner)
    {
        owner = default;
        return _owned is { } owned && owned.TryGetValue(number, out owner);
    }

    /// <summary>Reads an object's current and signed values when a later revision defined or freed it.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <param name="now">The current value, or null when freed.</param>
    /// <param name="before">The signed value, or null when the object is new.</param>
    /// <returns><see langword="true"/> when a later revision touched the object.</returns>
    private bool TryGetCandidate(int number, long signedEnd, out PdfValue now, out PdfValue before)
    {
        now = default;
        before = default;
        var offset = StoreRevisions.GetDefinitionOffset(_current, number);
        var signedOffset = number < _signed.XrefSize ? StoreRevisions.GetDefinitionOffset(_signed, number) : -1;
        if (offset < signedEnd && SameDefinition(number, offset, signedOffset))
        {
            return false;
        }

        now = offset >= 0 ? StoreReading.GetObject(_current, new(number, 0)) : default;
        before = signedOffset >= 0 ? StoreReading.GetObject(_signed, new(number, 0)) : default;
        return !now.IsNull || !before.IsNull;
    }

    /// <summary>Checks whether both tables select the same definition, including an object-stream member's index.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="offset">The current definition's file offset.</param>
    /// <param name="signedOffset">The signed definition's file offset.</param>
    /// <returns>Whether the selected definitions match.</returns>
    private bool SameDefinition(int number, long offset, long signedOffset)
    {
        if (offset != signedOffset)
        {
            return false;
        }

        if (offset < 0)
        {
            return true;
        }

        var current = _current.XrefState;
        var signed = _signed.XrefState;
        var type = current.GetType(number);
        if (type != signed.GetType(number))
        {
            return false;
        }

        // Direct entries with the same physical offset select the same bytes; generations are ignored when reading.
        return type != XrefEntryType.Compressed
            || (current.GetLocation(number) == signed.GetLocation(number) && current.GetDetail(number) == signed.GetDetail(number));
    }

    /// <summary>Classifies a changed object by what it is.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="now">The current value, or null when freed.</param>
    /// <param name="before">The signed value, or null when new.</param>
    /// <returns>The change, a change of kind None for file structure, or <see langword="null"/> when the object is not recognised.</returns>
    private PdfObjectChange? Classify(int number, PdfValue now, PdfValue before)
    {
        if (before.AsDictionary() is { } signedPage && KindOfTarget(signedPage, _tables.Right) == PdfModificationKinds.Pages)
        {
            // An unsigned replacement cannot change a signed page's role to gain annotation or metadata permissions.
            return new(number, SignedPageKind(now.AsDictionary(), signedPage), null, false);
        }

        if (number == _root)
        {
            return new(number, ClassifyCatalog(now.AsDictionary(), before.AsDictionary()), null, false);
        }

        if (number == _info)
        {
            return new(number, PdfModificationKinds.Metadata, null, false);
        }

        if (TryGetOwner(number, out var owner) && owner.Kind == PdfModificationKinds.SecurityStore)
        {
            return owner with
            {
                ObjectNumber = number
            };
        }

        var removed = now.IsNull;
        var value = removed ? before : now;
        var names = removed ? _tables.Right : _tables.Left;
        return value.Kind switch
        {
            PdfKind.Stream => ClassifyStream(number, value.AsStream()!),
            PdfKind.Dictionary => ClassifyDictionary(number, value.AsDictionary()!, names, now.AsDictionary(), before.AsDictionary()),
            PdfKind.Array => ClassifyArray(number, now.AsArray(), before.AsArray()),
            _ => null,
        };
    }

    /// <summary>Classifies a signed page without trusting the replacement's object type.</summary>
    /// <param name="now">The replacement dictionary, or null when removed or replaced with a scalar.</param>
    /// <param name="before">The signed page or page-tree dictionary.</param>
    /// <returns>Page changes, or the annotation changes of an otherwise unchanged page.</returns>
    private PdfModificationKinds SignedPageKind(PdfDictionary? now, PdfDictionary before) =>
        now is not null && now.IsName(KnownName.Type, KnownName.Page) && before.IsName(KnownName.Type, KnownName.Page)
            ? ClassifyPage(now, before)
            : PdfModificationKinds.Pages;

    /// <summary>Classifies a changed dictionary.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="dictionary">The current dictionary, or the signed one when it was removed.</param>
    /// <param name="names">The name table of <paramref name="dictionary"/>.</param>
    /// <param name="now">The current dictionary, or <see langword="null"/>.</param>
    /// <param name="before">The signed dictionary, or <see langword="null"/>.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    private PdfObjectChange? ClassifyDictionary(int number, PdfDictionary dictionary, PdfNameTable names, PdfDictionary? now, PdfDictionary? before)
    {
        if (PdfSignatureDictionaries.IsSignatureValue(dictionary, names))
        {
            var kind = PdfSignatureDictionaries.IsDocumentTimestamp(dictionary, names) ? PdfModificationKinds.SecurityStore : PdfModificationKinds.Signature;
            return new(number, kind, null, false);
        }

        if (dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            return new(number, now is null || before is null ? PdfModificationKinds.Pages : ClassifyPage(now, before), null, false);
        }

        if (dictionary.IsName(KnownName.Type, KnownName.Pages))
        {
            return new(number, PdfModificationKinds.Pages, null, false);
        }

        if (number == _acroForm)
        {
            return new(number, ClassifyAcroForm(now, before), null, false);
        }

        return IsAnnotation(dictionary) || IsField(dictionary) ? ClassifyWidgetOrField(number, dictionary, names) : null;
    }

    /// <summary>Classifies a changed array by the objects added to or removed from it.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="now">The current array, or <see langword="null"/>.</param>
    /// <param name="before">The signed array, or <see langword="null"/>.</param>
    /// <returns>The change, or <see langword="null"/> when no recognised object was added or removed.</returns>
    private PdfObjectChange? ClassifyArray(int number, PdfArray? now, PdfArray? before)
    {
        var kind = ReferenceDifference(now, before);
        return kind == PdfModificationKinds.None ? null : new(number, kind, null, false);
    }
}
