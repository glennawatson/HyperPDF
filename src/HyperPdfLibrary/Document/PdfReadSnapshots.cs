// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace HyperPdfLibrary.Document;

/// <summary>
/// The results of the document's read operations, kept until the next edit. An edit drops the whole set, so a result is
/// never older than the objects it was read from; a read that finishes after an edit stores into the dropped set.
/// </summary>
internal sealed class PdfReadSnapshots
{
    /// <summary>The number of kinds of read.</summary>
    private const int KindCount = (int)PdfReadKind.Count;

    /// <summary>
    /// The results by kind: one result for a read of the whole document, or a table of results by object for a read of one
    /// object. A kind is only ever read one way.
    /// </summary>
    private readonly object?[] _reads = new object?[KindCount];

    /// <summary>Returns a published non-null document-wide reference result without starting a read.</summary>
    /// <typeparam name="T">The reference result type.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <returns>The published result, or null when none is available.</returns>
    internal static T? GetCached<T>(PdfDocument document, PdfReadKind kind)
        where T : class =>
        Volatile.Read(ref document.State.Reads) is { } reads && Volatile.Read(ref reads._reads[(int)kind]) is T hit
            ? hit
            : null;

    /// <summary>Reads a document-wide result once and keeps it.</summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <param name="read">Reads the result from the document's objects.</param>
    /// <returns>The kept result.</returns>
    internal static T Read<T>(PdfDocument document, PdfReadKind kind, Func<PdfDocument, T> read)
    {
        var reads = PdfReadSnapshots.Current(document);
        if (Volatile.Read(ref reads._reads[(int)kind]) is Held<T> hit)
        {
            return hit.Value;
        }

        var held = new Held<T>(read(document));
        return ((Held<T>)(Interlocked.CompareExchange(ref reads._reads[(int)kind], held, null) ?? held)).Value;
    }

    /// <summary>Reads and keeps a non-null reference result whose initial construction can be cancelled.</summary>
    /// <typeparam name="T">The reference result type.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <param name="read">Constructs the result.</param>
    /// <param name="cancellationToken">Cancels construction.</param>
    /// <returns>The kept result.</returns>
    internal static T Read<T>(PdfDocument document, PdfReadKind kind, Func<PdfDocument, CancellationToken, T> read, CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reads = PdfReadSnapshots.Current(document);
        if (Volatile.Read(ref reads._reads[(int)kind]) is T hit)
        {
            return hit;
        }

        var result = read(document, cancellationToken);
        return (T)(Interlocked.CompareExchange(ref reads._reads[(int)kind], result, null) ?? result);
    }

    /// <summary>Reads and keeps a cancellable result with explicitly passed operation state.</summary>
    /// <typeparam name="TState">The operation state type.</typeparam>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <param name="state">The operation state.</param>
    /// <param name="read">Constructs the result.</param>
    /// <param name="cancellationToken">Cancels construction.</param>
    /// <returns>The kept result.</returns>
    internal static T Read<TState, T>(PdfDocument document, PdfReadKind kind, TState state, Func<PdfDocument, TState, CancellationToken, T> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reads = PdfReadSnapshots.Current(document);
        if (Volatile.Read(ref reads._reads[(int)kind]) is Held<T> hit)
        {
            return hit.Value;
        }

        var held = new Held<T>(read(document, state, cancellationToken));
        return ((Held<T>)(Interlocked.CompareExchange(ref reads._reads[(int)kind], held, null) ?? held)).Value;
    }

    /// <summary>Reads a result for one object once and keeps it.</summary>
    /// <typeparam name="TOwner">The type of the object the result is read from, compared by reference.</typeparam>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <param name="owner">The object the result is read from.</param>
    /// <param name="read">Reads the result from the object.</param>
    /// <returns>The kept result.</returns>
    internal static T Read<TOwner, T>(PdfDocument document, PdfReadKind kind, TOwner owner, Func<PdfDocument, TOwner, T> read)
        where TOwner : class
    {
        var results = PdfReadSnapshots.Results<TOwner, T>(document, kind);
        return results.TryGetValue(owner, out var hit) ? hit.Value : results.GetOrAdd(owner, new Held<T>(read(document, owner))).Value;
    }

    /// <summary>Reads a result for one object once and keeps it.</summary>
    /// <typeparam name="TOwner">The type of the object the result is read from, compared by reference.</typeparam>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <param name="owner">The object the result is read from.</param>
    /// <param name="read">Reads the result from the object.</param>
    /// <returns>The kept result.</returns>
    internal static T Read<TOwner, T>(PdfDocument document, PdfReadKind kind, TOwner owner, Func<TOwner, T> read)
        where TOwner : class
    {
        var results = PdfReadSnapshots.Results<TOwner, T>(document, kind);
        return results.TryGetValue(owner, out var hit) ? hit.Value : results.GetOrAdd(owner, new Held<T>(read(owner))).Value;
    }

    /// <summary>Reads one object's result with cancellation and retains it in the shared owner cache.</summary>
    /// <typeparam name="TOwner">The owner type, compared by reference.</typeparam>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The read kind.</param>
    /// <param name="owner">The owner object.</param>
    /// <param name="read">Constructs the result.</param>
    /// <param name="cancellationToken">Cancels construction.</param>
    /// <returns>The retained result.</returns>
    internal static T Read<TOwner, T>(PdfDocument document, PdfReadKind kind, TOwner owner, Func<TOwner, CancellationToken, T> read, CancellationToken cancellationToken)
        where TOwner : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = PdfReadSnapshots.Results<TOwner, T>(document, kind);
        return results.TryGetValue(owner, out var hit) ? hit.Value : results.GetOrAdd(owner, new Held<T>(read(owner, cancellationToken))).Value;
    }

    /// <summary>Gets the table of per-object results of a kind, starting it when there is none.</summary>
    /// <typeparam name="TOwner">The type of the object the results are read from.</typeparam>
    /// <typeparam name="T">The type of the results.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of read.</param>
    /// <returns>The table.</returns>
    private static ConcurrentDictionary<TOwner, Held<T>> Results<TOwner, T>(PdfDocument document, PdfReadKind kind)
        where TOwner : class
    {
        var reads = PdfReadSnapshots.Current(document);
        var slot = Volatile.Read(ref reads._reads[(int)kind]);
        if (slot is null)
        {
            var created = new ConcurrentDictionary<TOwner, Held<T>>(ReferenceEqualityComparer.Instance);
            slot = Interlocked.CompareExchange(ref reads._reads[(int)kind], created, null) ?? created;
        }

        return (ConcurrentDictionary<TOwner, Held<T>>)slot;
    }

    /// <summary>Gets the document's current set of results, starting an empty one after an edit dropped the last.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The set.</returns>
    private static PdfReadSnapshots Current(PdfDocument document)
    {
        var current = Volatile.Read(ref document.State.Reads);
        if (current is not null)
        {
            return current;
        }

        var created = new PdfReadSnapshots();
        return Interlocked.CompareExchange(ref document.State.Reads, created, null) ?? created;
    }

    /// <summary>A kept result, which may be <see langword="null"/>.</summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="Value">The result.</param>
    private sealed record Held<T>(T Value);
}
