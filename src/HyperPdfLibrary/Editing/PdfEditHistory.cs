// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// The undo and redo stacks of a store's committed transactions. Undo puts back the exact objects a transaction
/// replaced; redo puts back the ones it wrote. Committing a new transaction clears the redo stack. The undo stack keeps
/// at most <see cref="Depth"/> transactions, dropping the oldest.
/// </summary>
/// <remarks>Thread-safe: every member takes the store's lock.</remarks>
[DebuggerDisplay("PdfEditHistory: {UndoCount} undo, {RedoCount} redo")]
public sealed class PdfEditHistory
{
    /// <summary>The number of transactions kept for undo by default.</summary>
    private const int InitialDepth = 100;

    /// <summary>The store whose edits are tracked.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The transactions that can be undone, oldest first.</summary>
    private readonly List<PdfEditRecord> _undo = [];

    /// <summary>The transactions that can be redone, most recently undone last.</summary>
    private readonly List<PdfEditRecord> _redo = [];

    /// <summary>The most transactions kept for undo.</summary>
    private int _depth = InitialDepth;

    /// <summary>Initializes a new instance of the <see cref="PdfEditHistory"/> class.</summary>
    /// <param name="store">The store whose edits are tracked.</param>
    internal PdfEditHistory(PdfObjectStore store) => _store = store;

    /// <summary>Gets or sets the most transactions kept for undo; zero turns undo off. Lowering it drops the oldest.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int Depth
    {
        get
        {
            lock (_store.Gate)
            {
                return _depth;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_store.Gate)
            {
                _depth = value;
                Trim(_undo, value);
                Trim(_redo, value);
            }
        }
    }

    /// <summary>Gets the number of transactions that can be undone.</summary>
    public int UndoCount
    {
        get
        {
            lock (_store.Gate)
            {
                return _undo.Count;
            }
        }
    }

    /// <summary>Gets the number of transactions that can be redone.</summary>
    public int RedoCount
    {
        get
        {
            lock (_store.Gate)
            {
                return _redo.Count;
            }
        }
    }

    /// <summary>Gets the label of the transaction undo would revert, or <see langword="null"/>.</summary>
    public string? UndoLabel
    {
        get
        {
            lock (_store.Gate)
            {
                return _undo.Count > 0 ? _undo[^1].Label : null;
            }
        }
    }

    /// <summary>Gets the label of the transaction redo would apply again, or <see langword="null"/>.</summary>
    public string? RedoLabel
    {
        get
        {
            lock (_store.Gate)
            {
                return _redo.Count > 0 ? _redo[^1].Label : null;
            }
        }
    }

    /// <summary>Reverts the most recent committed transaction.</summary>
    /// <returns><see langword="true"/> when one was reverted.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Undo() => Step(_undo, _redo, true);

    /// <summary>Applies the most recently undone transaction again.</summary>
    /// <returns><see langword="true"/> when one was applied.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Redo() => Step(_redo, _undo, false);

    /// <summary>Forgets every transaction, for example after the document is saved and reloaded.</summary>
    public void Clear()
    {
        lock (_store.Gate)
        {
            _undo.Clear();
            _redo.Clear();
        }
    }

    /// <summary>Pushes a committed transaction, while holding the store's lock.</summary>
    /// <param name="record">The transaction.</param>
    internal void PushLocked(PdfEditRecord record)
    {
        _redo.Clear();
        if (_depth == 0)
        {
            return;
        }

        _undo.Add(record);
        Trim(_undo, _depth);
    }

    /// <summary>Drops the oldest records beyond a depth.</summary>
    /// <param name="records">The records, oldest first.</param>
    /// <param name="depth">The most kept.</param>
    private static void Trim(List<PdfEditRecord> records, int depth)
    {
        if (records.Count > depth)
        {
            records.RemoveRange(0, records.Count - depth);
        }
    }

    /// <summary>Moves one transaction from one stack to the other, applying its before or after state.</summary>
    /// <param name="from">The stack taken from.</param>
    /// <param name="to">The stack pushed to.</param>
    /// <param name="undo">Whether to apply the before state.</param>
    /// <returns><see langword="true"/> when a transaction was applied.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    private bool Step(List<PdfEditRecord> from, List<PdfEditRecord> to, bool undo)
    {
        PdfCacheScopes scopes;
        lock (_store.Gate)
        {
            if (StoreTransactions.GetCurrentTransaction(_store) is not null)
            {
                throw new InvalidOperationException("Undo and redo are not allowed while a transaction is open.");
            }

            if (from.Count == 0)
            {
                return false;
            }

            var record = from[^1];
            scopes = record.Scopes;
            from.RemoveAt(from.Count - 1);
            if (undo)
            {
                StoreTransactions.RestoreLocked(_store, record.Before, record.TrailerBefore);
            }
            else
            {
                StoreTransactions.RestoreLocked(_store, record.After, record.TrailerAfter);
            }

            to.Add(record);
            Trim(to, _depth);
        }

        StoreTransactions.NotifyChanged(_store, scopes);
        return true;
    }
}
