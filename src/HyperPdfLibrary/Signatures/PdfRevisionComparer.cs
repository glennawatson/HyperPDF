// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Signatures;

/// <summary>Finds and orders objects changed after a signed revision.</summary>
internal static class PdfRevisionComparer
{
    /// <summary>The mask for cancellation checks during bounded object walks.</summary>
    internal const int CancellationMask = 255;

    /// <summary>Lists the changes made after the signed revision.</summary>
    /// <param name="current">The document as it is now.</param>
    /// <param name="signed">The document as it was signed.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <returns>The changes, by object number; none is marked permitted yet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfObjectChange[] Compare(PdfObjectStore current, PdfObjectStore signed, long signedEnd)
    {
        var state = new PdfRevisionComparison(
            current,
            signed,
            new(current.Names, signed.Names),
            ReferenceNumber(current.Trailer.GetRaw(KnownName.Root)),
            ReferenceNumber(current.Trailer.GetRaw(KnownName.Info)),
            ReferenceNumber(current.Catalog.GetRaw(KnownName.AcroForm)),
            null,
            current.Context?.CancellationToken ?? CancellationToken.None);
        try
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            PdfRevisionOwnership.Own(ref state, current.Catalog.GetRaw(KnownName.DSS), new(0, PdfModificationKinds.SecurityStore, null, false));
            return Run(ref state, signedEnd);
        }
        finally
        {
            if (state.Owned is { } owned)
            {
                owned.Clear();
                _ = current.ScratchPools.Slots<Dictionary<int, PdfObjectChange>>().Offer(owned);
            }
        }
    }

    /// <summary>Gets the object number of a reference.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The number, or -1 when the value is not a reference.</returns>
    internal static int ReferenceNumber(PdfValue value) => value.IsReference ? value.AsReference().Number : -1;

    /// <summary>Compares every candidate object.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <returns>The changes.</returns>
    internal static PdfObjectChange[] Run(ref PdfRevisionComparison state, long signedEnd)
    {
        List<PdfObjectChange>? changes = null;
        List<int>? unresolved = null;
        try
        {
            for (var number = 1; number < state.Current.XrefSize; number++)
            {
                CheckCancellation(state, number);
                if (!TryGetCandidate(state, number, signedEnd, out var now, out var before)
                    || (!now.IsNull && !before.IsNull && PdfValueComparer.Equal(now, state.Tables.Left, before, state.Tables.Right)))
                {
                    continue;
                }

                if (PdfRevisionClassification.Classify(state, number, now, before) is not { } change)
                {
                    Defer(state.Current, ref unresolved, number);
                }
                else if (change.Kind != PdfModificationKinds.None)
                {
                    AddChange(state.Current, ref changes, change);
                    PdfRevisionOwnership.OwnParts(ref state, change, now);
                }
            }

            PdfRevisionComparer.CompleteUnresolved(state, unresolved, ref changes);
            return SnapshotChanges(changes);
        }
        finally
        {
            ReleaseChanges(state.Current, changes);
            ReleaseDeferred(state.Current, unresolved);
        }
    }

    /// <summary>Acquires result-building storage only when an object actually changed.</summary>
    /// <param name="current">The store owning temporary working storage.</param>
    /// <param name="changes">The result builder, created on first use.</param>
    /// <param name="change">The classified object.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddChange(PdfObjectStore current, ref List<PdfObjectChange>? changes, PdfObjectChange change) =>
        (changes ??= current.ScratchPools.Slots<List<PdfObjectChange>>().Take() ?? []).Add(change);

    /// <summary>Returns an ordered, detached snapshot, or the shared empty result.</summary>
    /// <param name="changes">The result builder, or null when nothing changed.</param>
    /// <returns>The changes ordered by object number.</returns>
    internal static PdfObjectChange[] SnapshotChanges(List<PdfObjectChange>? changes)
    {
        if (changes is null)
        {
            return [];
        }

        changes.Sort(static (left, right) => left.ObjectNumber.CompareTo(right.ObjectNumber));
        return changes.ToArray();
    }

    /// <summary>Clears result-building references before returning storage to its owner.</summary>
    /// <param name="current">The store owning temporary working storage.</param>
    /// <param name="changes">The leased result builder, or null when unused.</param>
    internal static void ReleaseChanges(PdfObjectStore current, List<PdfObjectChange>? changes)
    {
        if (changes is null)
        {
            return;
        }

        changes.Clear();
        _ = current.ScratchPools.Slots<List<PdfObjectChange>>().Offer(changes);
    }

    /// <summary>Checks cancellation once per batch of walked objects.</summary>
    /// <param name="state">The comparison and its explicit cancellation token.</param>
    /// <param name="count">The number of processed objects.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckCancellation(in PdfRevisionComparison state, int count)
    {
        if ((count & CancellationMask) == 0)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Defers an object until its possible owners have been classified.</summary>
    /// <param name="current">The store owning temporary working storage.</param>
    /// <param name="unresolved">Deferred object numbers, created on first use.</param>
    /// <param name="number">The object number to defer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Defer(PdfObjectStore current, ref List<int>? unresolved, int number) =>
        (unresolved ??= current.ScratchPools.Slots<List<int>>().Take() ?? []).Add(number);

    /// <summary>Returns cleared deferred-object storage to its owner.</summary>
    /// <param name="current">The store owning temporary working storage.</param>
    /// <param name="unresolved">The leased storage, or null when unused.</param>
    internal static void ReleaseDeferred(PdfObjectStore current, List<int>? unresolved)
    {
        if (unresolved is null)
        {
            return;
        }

        unresolved.Clear();
        _ = current.ScratchPools.Slots<List<int>>().Offer(unresolved);
    }

    /// <summary>Classifies deferred objects after all their potential owners have been visited.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="unresolved">Deferred object numbers, or null when every candidate was classified.</param>
    /// <param name="changes">The classified changes.</param>
    internal static void CompleteUnresolved(in PdfRevisionComparison state, List<int>? unresolved, ref List<PdfObjectChange>? changes)
    {
        if (unresolved is null)
        {
            return;
        }

        foreach (var number in unresolved)
        {
            var change = PdfRevisionComparer.TryGetOwner(state, number, out var owner)
                ? owner with { ObjectNumber = number }
                : new(number, PdfModificationKinds.Other, null, false);
            AddChange(state.Current, ref changes, change);
        }
    }

    /// <summary>Reads a change's ownership only when a reachable-object walk created the index.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="owner">The change that owns the object.</param>
    /// <returns>Whether an owner is recorded.</returns>
    internal static bool TryGetOwner(in PdfRevisionComparison state, int number, out PdfObjectChange owner)
    {
        owner = default;
        return state.Owned is { } owned && owned.TryGetValue(number, out owner);
    }

    /// <summary>Reads an object's current and signed values when a later revision defined or freed it.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <param name="now">The current value, or null when freed.</param>
    /// <param name="before">The signed value, or null when the object is new.</param>
    /// <returns><see langword="true"/> when a later revision touched the object.</returns>
    internal static bool TryGetCandidate(in PdfRevisionComparison state, int number, long signedEnd, out PdfValue now, out PdfValue before)
    {
        now = default;
        before = default;
        var offset = StoreRevisions.GetDefinitionOffset(state.Current, number);
        var signedOffset = number < state.Signed.XrefSize ? StoreRevisions.GetDefinitionOffset(state.Signed, number) : -1;
        if (offset < signedEnd && PdfRevisionComparer.SameDefinition(state, number, offset, signedOffset))
        {
            return false;
        }

        now = offset >= 0 ? StoreReading.GetObject(state.Current, new(number, 0)) : default;
        before = signedOffset >= 0 ? StoreReading.GetObject(state.Signed, new(number, 0)) : default;
        return !now.IsNull || !before.IsNull;
    }

    /// <summary>Checks whether both tables select the same definition, including an object-stream member's index.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="offset">The current definition's file offset.</param>
    /// <param name="signedOffset">The signed definition's file offset.</param>
    /// <returns>Whether the selected definitions match.</returns>
    internal static bool SameDefinition(in PdfRevisionComparison state, int number, long offset, long signedOffset)
    {
        if (offset != signedOffset)
        {
            return false;
        }

        if (offset < 0)
        {
            return true;
        }

        var current = state.Current.XrefState;
        var signed = state.Signed.XrefState;
        var type = current.GetType(number);
        if (type != signed.GetType(number))
        {
            return false;
        }

        // Direct entries with the same physical offset select the same bytes; generations are ignored when reading.
        return type != XrefEntryType.Compressed
            || (current.GetLocation(number) == signed.GetLocation(number) && current.GetDetail(number) == signed.GetDetail(number));
    }
}
