// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Objects;

/// <summary>Records and restores transactional changes to the store.</summary>
public static class StoreTransactions
{
    /// <summary>Gets the undo and redo stacks of committed transactions.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The committed edit history.</returns>
    public static PdfEditHistory GetHistory(PdfObjectStore self)
    {
        if (Volatile.Read(ref self.HistoryState) is { } history)
        {
            return history;
        }

        _ = Interlocked.CompareExchange(ref self.HistoryState, new(self), null);
        return Volatile.Read(ref self.HistoryState)!;
    }

    /// <summary>Gets the open transaction, or <see langword="null"/> when none is open.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The open transaction, or null.</returns>
    public static PdfEditTransaction? GetCurrentTransaction(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            return self.TransactionState;
        }
    }

    /// <summary>
    /// Starts a transaction. Every change to the store until it ends, made through it or not, is recorded so it can be
    /// rolled back, or undone after it is committed.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "label">A short description of the edit, shown for undo.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "label"/> is <see langword="null"/>.</exception>
    /// <exception cref = "InvalidOperationException">A transaction is already open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEditTransaction BeginTransaction(PdfObjectStore self, string label) =>StoreTransactions.BeginTransaction(self, label, PdfCacheScopes.All);

    /// <summary>Starts a transaction whose changes are known to affect only some parts of the document.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "label">A short description of the edit, shown for undo.</param>
    /// <param name = "scopes">The parts of the document the transaction's changes can affect.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "label"/> is <see langword="null"/>.</exception>
    /// <exception cref = "InvalidOperationException">A transaction is already open.</exception>
    internal static PdfEditTransaction BeginTransaction(PdfObjectStore self, string label, PdfCacheScopes scopes)
    {
        ArgumentNullException.ThrowIfNull(label);
        lock (self.Gate)
        {
            if (self.TransactionState is not null)
            {
                throw new InvalidOperationException("A transaction is already open on this document.");
            }

            var transaction = new PdfEditTransaction(self, label, scopes);
            self.TransactionState = transaction;
            return transaction;
        }
    }

    /// <summary>Sets the listener told after a transaction ends or an edit is undone or redone.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "listener">The listener.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetChangeListener(PdfObjectStore self, IObjectsChangedListener listener) => Volatile.Write(ref self.ChangedState, listener);

    /// <summary>Gets the number the next added object gets.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The object number.</returns>
    internal static int GetNextNumber(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            return self.NextNumberState;
        }
    }

    /// <summary>
    /// Puts the number the next added object gets back to an earlier value, after a rollback removed every object added since,
    /// so the objects added next are numbered as if the rolled back ones never existed.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The value <see cref = "GetNextNumber"/> returned before the rolled back objects were added.</param>
    internal static void ResetNextNumber(PdfObjectStore self, int number)
    {
        lock (self.Gate)
        {
            self.NextNumberState = Math.Min(self.NextNumberState, number);
        }
    }

    /// <summary>Tells the change listener. Called without holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "scopes">The parts of the document the change can affect.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotifyChanged(PdfObjectStore self, PdfCacheScopes scopes) => Volatile.Read(ref self.ChangedState)?.OnObjectsChanged(scopes);

    /// <summary>Closes a transaction, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "transaction">The transaction.</param>
    internal static void EndTransactionLocked(PdfObjectStore self, PdfEditTransaction transaction)
    {
        if (ReferenceEquals(self.TransactionState, transaction))
        {
            self.TransactionState = null;
        }
    }

    /// <summary>Gets an object's entry in the edit layer, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The entry.</returns>
    internal static PdfObjectState CaptureLocked(PdfObjectStore self, int number)
    {
        var freed = self.FreedGenerationsIfCreated is { } generations && generations.TryGetValue(number, out var generation) ? generation : -1;
        if (self.EditsIfCreated is { } edits && edits.TryGetValue(number, out var value))
        {
            return new(number, PdfObjectStateKind.Edited, value, freed);
        }

        return new(number, self.DeletedIfCreated?.Contains(number) == true ? PdfObjectStateKind.Deleted : PdfObjectStateKind.Original, default, freed);
    }

    /// <summary>Puts objects and trailer entries back as they were captured, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "states">The object entries, applied last to first.</param>
    /// <param name = "trailer">The trailer entries, applied last to first.</param>
    internal static void RestoreLocked(PdfObjectStore self, ReadOnlySpan<PdfObjectState> states, ReadOnlySpan<PdfTrailerEntry> trailer)
    {
        for (var i = states.Length - 1; i >= 0; i--)
        {
            StoreTransactions.RestoreLocked(self, states[i]);
        }

        for (var i = trailer.Length - 1; i >= 0; i--)
        {
            StoreTransactions.SetTrailerLocked(self, trailer[i]);
        }

        StoreTransactions.RefreshCatalogLocked(self);
    }

    /// <summary>Puts one object back as captured, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "state">The entry.</param>
    internal static void RestoreLocked(PdfObjectStore self, in PdfObjectState state)
    {
        var number = state.Number;
        StoreTransactions.RecordEdit(self, number);
        switch (state.Kind)
        {
            case PdfObjectStateKind.Edited:
                {
                    self.EditsState[number] = state.Value;
                    _ = self.DeletedIfCreated?.Remove(number);
                    break;
                }

            case PdfObjectStateKind.Deleted:
                {
                    _ = self.EditsIfCreated?.Remove(number);
                    _ = self.DeletedState.Add(number);
                    break;
                }

            default:
                {
                    _ = self.EditsIfCreated?.Remove(number);
                    _ = self.DeletedIfCreated?.Remove(number);
                    break;
                }
        }

        if (state.FreedGeneration >= 0)
        {
            self.FreedGenerationsState[number] = state.FreedGeneration;
        }
        else
        {
            _ = self.FreedGenerationsIfCreated?.Remove(number);
        }

        self.NextNumberState = Math.Max(self.NextNumberState, number + 1);
        if ((uint)number < (uint)self.CacheState.Length)
        {
            Volatile.Write(ref self.CacheState[number], null);
        }
    }

    /// <summary>Sets or removes a trailer entry, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "entry">The key and value; a null value removes the key.</param>
    internal static void SetTrailerLocked(PdfObjectStore self, in PdfTrailerEntry entry)
    {
        self.TrailerEditedState = true;
        if (entry.Value.IsNull)
        {
            _ = self.Trailer.Remove(entry.Key);
            return;
        }

        self.Trailer.Set(entry.Key, entry.Value);
    }

    /// <summary>Reads the catalog again after the object holding it was replaced, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    internal static void RefreshCatalogLocked(PdfObjectStore self)
    {
        if (self.Trailer.GetDictionary(KnownName.Root) is { } catalog)
        {
            self.Catalog = catalog;
        }
    }

    /// <summary>Gets an object as the file holds it, ignoring edits.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>A freshly parsed value; null when the file has no such object.</returns>
    internal static PdfValue GetOriginal(PdfObjectStore self, int number)
    {
        if (number <= 0)
        {
            return default;
        }

        lock (self.Gate)
        {
            return StoreReading.TryLoad(self, number, out var value) ? value : default;
        }
    }

    /// <summary>Records an object's entry in the open transaction before it changes, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordEdit(PdfObjectStore self, int number)
    {
        // Only the first change to an object is recorded, so a repeat skips reading its entry.
        if (self.TransactionState is { } transaction && transaction.NeedsRecordLocked(number))
        {
            transaction.RecordLocked(StoreTransactions.CaptureLocked(self, number));
        }
    }
}
