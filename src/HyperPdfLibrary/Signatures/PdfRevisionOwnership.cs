// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Tracks objects that serve an annotation, form or security-store change.</summary>
internal static class PdfRevisionOwnership
{
    /// <summary>The most objects walked to find what a change owns.</summary>
    internal const int MaxOwnedObjects = 1 << 16;

    /// <summary>Marks the objects that serve a change: an annotation's appearances and icons, or the form's resources.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="change">The change.</param>
    /// <param name="now">The changed object's current value.</param>
    internal static void OwnParts(ref PdfRevisionComparison state, PdfObjectChange change, PdfValue now)
    {
        if (now.AsDictionary() is not { } dictionary)
        {
            return;
        }

        if (change.ObjectNumber == state.AcroForm)
        {
            PdfRevisionOwnership.Own(ref state, dictionary.GetRaw(KnownName.DR), change);
            return;
        }

        PdfRevisionOwnership.Own(ref state, dictionary.GetRaw(KnownName.AP), change);
        PdfRevisionOwnership.Own(ref state, dictionary.GetRaw(KnownName.MK), change);
    }

    /// <summary>Marks every object reachable from a value as serving a change, unless already marked.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="start">The value.</param>
    /// <param name="change">The change.</param>
    internal static void Own(ref PdfRevisionComparison state, PdfValue start, PdfObjectChange change)
    {
        if (start.IsNull)
        {
            return;
        }

        var owned = state.Owned;
        if (owned is null)
        {
            owned = state.Current.ScratchPools.Slots<Dictionary<int, PdfObjectChange>>().Take() ?? [];
            state = state with { Owned = owned };
        }

        var pending = state.Current.ScratchPools.Slots<Stack<PdfValue>>().Take() ?? new();
        try
        {
            pending.Push(start);
            var walked = 0;
            while (pending.Count > 0 && owned.Count < MaxOwnedObjects)
            {
                PdfRevisionComparer.CheckCancellation(state, walked);
                walked++;
                var value = pending.Pop();
                if (value.IsReference)
                {
                    if (owned.TryAdd(value.AsReference().Number, change))
                    {
                        pending.Push(StoreReading.GetObject(state.Current, value.AsReference()));
                    }

                    continue;
                }

                PdfRevisionOwnership.PushChildren(pending, value);
            }
        }
        finally
        {
            pending.Clear();
            _ = state.Current.ScratchPools.Slots<Stack<PdfValue>>().Offer(pending);
        }
    }

    /// <summary>Queues the values inside an array, dictionary or stream.</summary>
    /// <param name="pending">The queue.</param>
    /// <param name="value">The value.</param>
    internal static void PushChildren(Stack<PdfValue> pending, PdfValue value)
    {
        if (value.AsArray() is { } array)
        {
            foreach (var item in array.Items)
            {
                pending.Push(item);
            }

            return;
        }

        if (value.AsDictionary() is not { } dictionary)
        {
            return;
        }

        for (var i = 0; i < dictionary.Count; i++)
        {
            pending.Push(dictionary.GetValueAt(i));
        }
    }
}
