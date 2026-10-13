// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// A group of edits to a <see cref="PdfObjectStore"/> that is committed or rolled back as one. While it is open, the
/// store records each object's edit-layer entry the first time it changes, from any caller, so rollback and undo put
/// back the exact previous objects. Values are recorded by reference: change a copy (for example
/// <see cref="CloneDictionary"/>) and replace the object with it, never a value the store already holds.
/// </summary>
/// <remarks>Thread-safe: every member takes the store's lock. Disposing an open transaction rolls it back.</remarks>
[DebuggerDisplay("PdfEditTransaction: {Label}")]
public sealed class PdfEditTransaction : IDisposable
{
    /// <summary>The state of an open transaction.</summary>
    private const int OpenState = 0;

    /// <summary>The state of a committed transaction.</summary>
    private const int CommittedState = 1;

    /// <summary>The state of a rolled back transaction.</summary>
    private const int RolledBackState = 2;

    /// <summary>The most recorded objects checked with a vectorised scan before a hash set takes over.</summary>
    private const int ScanLimit = 64;

    /// <summary>The store being edited.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The object numbers already recorded once there are more than <see cref="ScanLimit"/>; made on demand.</summary>
    private HashSet<int>? _touched;

    /// <summary>The objects as they were before their first change, in order; a pooled array held until the transaction ends.</summary>
    private PdfObjectState[]? _before;

    /// <summary>The numbers of the entries in <see cref="_before"/>, in the same order; a pooled array held until the transaction ends.</summary>
    private int[]? _beforeNumbers;

    /// <summary>The number of objects recorded.</summary>
    private int _beforeCount;

    /// <summary>The trailer entries as they were before their first change, in order; a pooled array held until the transaction ends.</summary>
    private PdfTrailerEntry[]? _trailerBefore;

    /// <summary>The number of trailer entries recorded.</summary>
    private int _trailerCount;

    /// <summary>Open, committed or rolled back.</summary>
    private int _state;

    /// <summary>The parts of the document the changes can affect; changed only while holding the store's lock.</summary>
    private PdfCacheScopes _scopes;

    /// <summary>Initializes a new instance of the <see cref="PdfEditTransaction"/> class.</summary>
    /// <param name="store">The store being edited.</param>
    /// <param name="label">The description.</param>
    /// <param name="scopes">The parts of the document the changes can affect.</param>
    internal PdfEditTransaction(PdfObjectStore store, string label, PdfCacheScopes scopes)
    {
        _store = store;
        Label = label;
        _scopes = scopes;
        OwnerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>Gets the description of the edit.</summary>
    public string Label { get; }

    /// <summary>Gets the kinds of change the transaction declared.</summary>
    public PdfChangeKinds Kinds { get; private set; }

    /// <summary>Gets a value indicating whether the transaction is still open.</summary>
    public bool IsOpen => Volatile.Read(ref _state) == OpenState;

    /// <summary>Gets the number of objects changed so far.</summary>
    public int ChangedCount
    {
        get
        {
            lock (_store.Gate)
            {
                return _beforeCount;
            }
        }
    }

    /// <summary>Gets the managed id of the thread that started the transaction; edits made on other threads do not join it.</summary>
    internal int OwnerThreadId { get; }

    /// <summary>Adds a new indirect object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The new object's id.</returns>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public PdfObjectId Add(PdfValue value)
    {
        ThrowIfEnded();
        return StoreEditing.Add(_store, value);
    }

    /// <summary>Replaces an indirect object.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Replace(PdfObjectId id, PdfValue value)
    {
        ThrowIfEnded();
        StoreEditing.Replace(_store, id, value);
    }

    /// <summary>Deletes an indirect object.</summary>
    /// <param name="id">The object id.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Delete(PdfObjectId id)
    {
        ThrowIfEnded();
        StoreEditing.Delete(_store, id);
    }

    /// <summary>Copies an object's dictionary for editing; nested values are shared, so copy them before changing them too.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The copy, or <see langword="null"/> when the object is not a dictionary.</returns>
    public PdfDictionary? CloneDictionary(PdfObjectId id) => StoreReading.GetObject(_store, id) is { Kind: PdfKind.Dictionary } value ? value.AsDictionary()!.Clone() : null;

    /// <summary>Sets or removes a trailer entry, such as /Info.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; null removes the key.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void SetTrailerEntry(PdfName key, PdfValue value)
    {
        lock (_store.Gate)
        {
            ThrowIfEnded();
            if (!IsTrailerRecorded(key.Id))
            {
                var entries = PooledArrays.EnsureRoom(ref _trailerBefore, _trailerCount, clear: true);
                entries[_trailerCount] = new(key, _store.Trailer.GetRaw(key));
                _trailerCount++;
            }

            StoreTransactions.SetTrailerLocked(_store, new(key, value));
        }
    }

    /// <summary>Declares kinds of change the transaction makes, for signature reports and undo labels.</summary>
    /// <param name="kinds">The kinds.</param>
    public void AddKinds(PdfChangeKinds kinds)
    {
        lock (_store.Gate)
        {
            Kinds |= kinds;
        }
    }

    /// <summary>Ends the transaction, keeping its changes and pushing it onto the undo stack when it changed anything.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Commit()
    {
        PdfCacheScopes scopes;
        lock (_store.Gate)
        {
            ThrowIfEnded();
            scopes = _scopes;
            var before = _beforeCount > 0 ? _before.AsSpan(0, _beforeCount).ToArray() : [];
            var after = before.Length == 0 ? [] : new PdfObjectState[before.Length];
            for (var i = 0; i < before.Length; i++)
            {
                after[i] = StoreTransactions.CaptureLocked(_store, before[i].Number);
            }

            var trailerBefore = _trailerCount > 0 ? _trailerBefore.AsSpan(0, _trailerCount).ToArray() : [];
            var trailerAfter = trailerBefore.Length == 0 ? [] : new PdfTrailerEntry[trailerBefore.Length];
            for (var i = 0; i < trailerBefore.Length; i++)
            {
                trailerAfter[i] = new(trailerBefore[i].Key, _store.Trailer.GetRaw(trailerBefore[i].Key));
            }

            ReleaseLog();
            _state = CommittedState;
            StoreTransactions.EndTransactionLocked(_store, this);
            StoreTransactions.RefreshCatalogLocked(_store);
            if (before.Length > 0 || trailerBefore.Length > 0)
            {
                StoreTransactions.GetHistory(_store).PushLocked(new(Label, Kinds, scopes, before, after, trailerBefore, trailerAfter));
            }
        }

        StoreTransactions.NotifyChanged(_store, scopes);
    }

    /// <summary>Ends the transaction, putting back every object and trailer entry it changed.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Rollback()
    {
        PdfCacheScopes scopes;
        lock (_store.Gate)
        {
            ThrowIfEnded();
            scopes = _scopes;
            RollbackLocked();
        }

        StoreTransactions.NotifyChanged(_store, scopes);
    }

    /// <summary>Rolls the transaction back when it is still open.</summary>
    public void Dispose()
    {
        PdfCacheScopes scopes;
        lock (_store.Gate)
        {
            if (_state != OpenState)
            {
                return;
            }

            scopes = _scopes;
            RollbackLocked();
        }

        StoreTransactions.NotifyChanged(_store, scopes);
    }

    /// <summary>Adds parts of the document the changes can affect, so caches built from them are dropped when the transaction ends.</summary>
    /// <param name="scopes">The parts.</param>
    internal void AddScopes(PdfCacheScopes scopes)
    {
        lock (_store.Gate)
        {
            _scopes |= scopes;
        }
    }

    /// <summary>Determines whether an object's earlier state still has to be recorded, while holding the store's lock.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>True when the transaction is open and has not recorded the object.</returns>
    internal bool NeedsRecordLocked(int number) => _state == OpenState && !IsRecorded(number);

    /// <summary>Records an object's entry before its first change, while holding the store's lock.</summary>
    /// <param name="state">The entry.</param>
    internal void RecordLocked(in PdfObjectState state)
    {
        if (_state != OpenState || IsRecorded(state.Number))
        {
            return;
        }

        var entries = PooledArrays.EnsureRoom(ref _before, _beforeCount, clear: true);
        var numbers = PooledArrays.EnsureRoom(ref _beforeNumbers, _beforeCount, clear: false);
        entries[_beforeCount] = state;
        numbers[_beforeCount] = state.Number;
        _beforeCount++;
        if (_touched is not null)
        {
            _ = _touched.Add(state.Number);
        }
        else if (_beforeCount > ScanLimit)
        {
            _touched = [.. numbers.AsSpan(0, _beforeCount)];
        }
    }

    /// <summary>Puts everything back and ends the transaction, while holding the store's lock.</summary>
    private void RollbackLocked()
    {
        _state = RolledBackState;
        StoreTransactions.RestoreLocked(_store, _before.AsSpan(0, _beforeCount), _trailerBefore.AsSpan(0, _trailerCount));
        ReleaseLog();
        StoreTransactions.EndTransactionLocked(_store, this);
    }

    /// <summary>Determines whether an object's earlier state was already recorded.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>True when it was.</returns>
    private bool IsRecorded(int number) => _touched is not null ? _touched.Contains(number) : _beforeNumbers.AsSpan(0, _beforeCount).Contains(number);

    /// <summary>Determines whether a trailer key's earlier value was already recorded.</summary>
    /// <param name="id">The key's name id.</param>
    /// <returns>True when it was.</returns>
    private bool IsTrailerRecorded(int id)
    {
        foreach (var entry in _trailerBefore.AsSpan(0, _trailerCount))
        {
            if (entry.Key.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gives the pooled log arrays back and drops the lookup set; the counts stay for <see cref="ChangedCount"/>.</summary>
    private void ReleaseLog()
    {
        PooledArrays.Return(ref _before, _beforeCount, clear: true);
        PooledArrays.Return(ref _beforeNumbers, _beforeCount, clear: false);
        PooledArrays.Return(ref _trailerBefore, _trailerCount, clear: true);
        _touched = null;
    }

    /// <summary>Throws when the transaction has ended.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    private void ThrowIfEnded()
    {
        if (Volatile.Read(ref _state) != OpenState)
        {
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        }
    }
}
