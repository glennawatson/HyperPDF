// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// A private copy of a document that the build attempts of one signature use in turn. The attempts differ only in the room
/// reserved for the signature value, so each attempt's edits are rolled back before the next, and the copy's cross-reference
/// data is read once. The copy keeps the document the caller shows from being edited, keeps a signature in progress on
/// another thread out of this one's transaction, and keeps a concurrent edit of the document from being rolled back with it.
/// </summary>
internal sealed class PdfSigningWorkspace : IDisposable
{
    /// <summary>The original caller's reusable storage owner.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>1 once the copy is disposed.</summary>
    private int _disposed;

    /// <summary>The reservations kept until the working copy has been disposed.</summary>
    private byte[][]? _reservations;

    /// <summary>The populated reservation slots.</summary>
    private int _reservationCount;

    /// <summary>Initializes a new instance of the <see cref="PdfSigningWorkspace"/> class.</summary>
    /// <param name="working">The private copy.</param>
    /// <param name="scratchPools">The original caller's reusable storage owner.</param>
    private PdfSigningWorkspace(PdfDocument working, ScratchPools scratchPools)
    {
        Document = working;
        _scratchPools = scratchPools;
    }

    /// <summary>Gets the private copy.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the original caller's native-context owner.</summary>
    internal ScratchPools ScratchPools => _scratchPools;

    /// <summary>Disposes the private copy, once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                Document.Dispose();
            }
            finally
            {
                if (_reservations is { } reservations)
                {
                    for (var i = 0; i < _reservationCount; i++)
                    {
                        _scratchPools.Bytes.Return(reservations[i], true);
                    }

                    _scratchPools.Get<byte[]>().Return(reservations, true);
                    _reservations = null;
                    _reservationCount = 0;
                }
            }
        }
    }

    /// <summary>Makes a private copy of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The workspace, which the caller disposes.</returns>
    internal static PdfSigningWorkspace Open(PdfDocument document) => new(PdfDocumentOptimizing.OpenWorkingCopy(document), document.Objects.ScratchPools);

    /// <summary>Leases zero-filled contents that remain owned until the working copy is disposed.</summary>
    /// <param name="capacity">The reserved signature bytes.</param>
    /// <param name="cancellationToken">Cancels before allocating a reservation.</param>
    /// <returns>The explicit reservation range.</returns>
    /// <exception cref="PdfException">The reservation cannot be represented in a PDF byte buffer.</exception>
    /// <exception cref="ObjectDisposedException">The workspace was disposed.</exception>
    internal PdfSignatureContents Reserve(int capacity, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        _ = PdfContentsSizing.RoundUp(capacity);
        if (_reservations is { } kept)
        {
            for (var i = 0; i < _reservationCount; i++)
            {
                if (kept[i].Length >= capacity)
                {
                    return new(kept[i], capacity);
                }
            }
        }

        EnsureReservationSlot();
        var buffer = _scratchPools.Bytes.Rent(capacity);
        buffer.AsSpan().Clear();
        _reservations![_reservationCount] = buffer;
        _reservationCount++;
        return new(buffer, capacity);
    }

    /// <summary>Starts one build attempt. Disposing the attempt rolls back everything it changed.</summary>
    /// <param name="label">The transaction's description.</param>
    /// <returns>The attempt.</returns>
    internal PdfSigningAttempt BeginAttempt(string label)
    {
        var store = Document.Objects;
        var next = StoreTransactions.GetNextNumber(store);
        return new(PdfDocumentEditing.BeginEdit(Document, label), store, next);
    }

    /// <summary>Grows the leased reservation index without releasing any contents it references.</summary>
    private void EnsureReservationSlot()
    {
        if (_reservations is { } existing && _reservationCount < existing.Length)
        {
            return;
        }

        var pool = _scratchPools.Get<byte[]>();
        var next = pool.Rent(_reservationCount + 1);
        if (_reservations is { } previous)
        {
            previous.AsSpan(0, _reservationCount).CopyTo(next);
            pool.Return(previous, true);
        }

        _reservations = next;
    }
}
