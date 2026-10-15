// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// The objects reachable from a trailer, numbered densely from 1 in breadth-first order so the catalog comes first.
/// Unreachable, free and missing objects get no number.
/// </summary>
[DebuggerDisplay("PdfObjectGraph: {Count} objects")]
internal sealed class PdfObjectGraph
{
    /// <summary>Checks cancellation after each 128 visited values.</summary>
    private const int CancellationCheckMask = 127;

    /// <summary>The save-graph cache bit for retaining unreachable objects.</summary>
    private const int KeepUnreferencedFlag = 2;

    /// <summary>The number of encryption and reachability option combinations.</summary>
    private const int SaveOptionCount = 4;

    /// <summary>The objects read from.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The old number of each new number, less one.</summary>
    private int[] _numbers = [];

    /// <summary>The value of each new number, less one.</summary>
    private PdfValue[] _values = [];

    /// <summary>Initializes a new instance of the <see cref="PdfObjectGraph"/> class.</summary>
    /// <param name="store">The objects read from.</param>
    private PdfObjectGraph(PdfObjectStore store)
    {
        _store = store;

        // One extra slot for a page tree root the repairs may have to make.
        Map = new int[store.Size + 1];
    }

    /// <summary>Gets the new number of each old number: zero for unreached objects, -1 for missing ones.</summary>
    internal int[] Map { get; }

    /// <summary>Gets the name table the objects' names come from.</summary>
    internal PdfNameTable Names => _store.Names;

    /// <summary>Gets the number of objects reached.</summary>
    internal int Count => _numbers.Length;

    /// <summary>Gets the completed graph for these save options until an object or trailer edit invalidates it.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="keepEncrypt">Whether the original encryption dictionary is kept.</param>
    /// <param name="keepUnreferenced">Whether unreachable objects are kept.</param>
    /// <param name="cancellationToken">Cancels graph construction.</param>
    /// <returns>The immutable completed graph.</returns>
    internal static PdfObjectGraph CollectForSave(PdfObjectStore store, bool keepEncrypt, bool keepUnreferenced, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (store.Gate)
        {
            ObjectDisposedException.ThrowIf(store.IsDisposed, store);
            var graphs = store.SaveGraphsState ??= new PdfObjectGraph?[SaveOptionCount];
            var index = (keepEncrypt ? 1 : 0) | (keepUnreferenced ? KeepUnreferencedFlag : 0);
            return graphs[index] ??= Collect(store, keepEncrypt, keepUnreferenced, cancellationToken);
        }
    }

    /// <summary>Walks the objects reachable from a trailer's /Root and /Info, and /Encrypt when kept.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="keepEncrypt">Whether /Encrypt is kept.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="PdfException">The trailer's /Root does not resolve to a dictionary.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfObjectGraph Collect(PdfObjectStore store, bool keepEncrypt, CancellationToken cancellationToken = default) =>
        Collect(store, keepEncrypt, false, cancellationToken);

    /// <summary>
    /// Walks the objects reachable from a trailer's /Root and /Info, and /Encrypt when kept, and optionally every other
    /// object in use after them, in object number order.
    /// </summary>
    /// <param name="store">The objects.</param>
    /// <param name="keepEncrypt">Whether /Encrypt is kept.</param>
    /// <param name="keepUnreferenced">
    /// Whether to also number objects no reference reaches. Object streams, cross-reference streams, a linearization
    /// dictionary and an /Encrypt dictionary that is not kept are structure of the source file, not content, and are skipped.
    /// </param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="PdfException">The trailer's /Root does not resolve to a dictionary.</exception>
    internal static PdfObjectGraph Collect(PdfObjectStore store, bool keepEncrypt, bool keepUnreferenced, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var walk = new Walk(store, cancellationToken);
        try
        {
            var graph = new PdfObjectGraph(store);
            var trailer = store.Trailer;
            graph.Scan(trailer.GetRaw(KnownName.Root), 0, ref walk);
            if (StoreReading.Resolve(store, trailer.GetRaw(KnownName.Root)).AsDictionary() is null)
            {
                throw new PdfException(PdfError.Format, "The document's /Root does not resolve to a catalog.");
            }

            graph.Scan(trailer.GetRaw(KnownName.Info), 0, ref walk);
            if (keepEncrypt)
            {
                graph.Scan(trailer.GetRaw(KnownName.Encrypt), 0, ref walk);
            }

            // The list grows while it is walked: a breadth-first queue.
            var walked = graph.Drain(0, ref walk);
            if (keepUnreferenced)
            {
                graph.AddUnreferenced(keepEncrypt ? 0 : PdfXrefWriter.EncryptNumber(trailer), walked, ref walk);
            }

            graph._numbers = [.. walk.Numbers];
            graph._values = [.. walk.Values];
            return graph;
        }
        finally
        {
            walk.Dispose();
        }
    }

    /// <summary>Gets the old number of an object.</summary>
    /// <param name="newNumber">The new number, from 1.</param>
    /// <returns>The old number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetOldNumber(int newNumber) => _numbers[newNumber - 1];

    /// <summary>Gets the value of an object.</summary>
    /// <param name="newNumber">The new number, from 1.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfValue GetValue(int newNumber) => _values[newNumber - 1];

    /// <summary>Determines whether an object is structure of the file it was read from rather than content: an object stream, a cross-reference stream or a linearization dictionary.</summary>
    /// <param name="value">The object.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool IsFileStructure(PdfValue value) =>
        value.AsDictionary() is { } dictionary
        && (dictionary.IsName(KnownName.Type, KnownName.ObjStm) || dictionary.IsName(KnownName.Type, KnownName.XRef) || dictionary.ContainsKey(_store.Names.Intern("Linearized"u8)));

    /// <summary>Scans the numbered objects from a position to the end of the list, numbering what they reach.</summary>
    /// <param name="next">The first object to scan, as an index into the list.</param>
    /// <param name="walk">The construction state.</param>
    /// <returns>The index after the last object scanned.</returns>
    private int Drain(int next, ref Walk walk)
    {
        for (; next < walk.Count; next++)
        {
            Scan(walk.Values[next], 0, ref walk);
        }

        return next;
    }

    /// <summary>Numbers the objects in use that no reference reached, with everything each of them reaches.</summary>
    /// <param name="skippedEncrypt">The number of an /Encrypt dictionary that is not kept, or zero.</param>
    /// <param name="walked">The index after the last object already scanned.</param>
    /// <param name="walk">The construction state.</param>
    private void AddUnreferenced(int skippedEncrypt, int walked, ref Walk walk)
    {
        for (var number = 1; number < _store.Size; number++)
        {
            walk.CancellationToken.ThrowIfCancellationRequested();
            if (Map[number] != 0 || number == skippedEncrypt || IsFileStructure(StoreReading.GetObject(_store, new(number, 0))))
            {
                continue;
            }

            Visit(number, ref walk);
            walked = Drain(walked, ref walk);
        }
    }

    /// <summary>Finds the references in a value and numbers the objects they reach.</summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="walk">The construction state.</param>
    private void Scan(PdfValue value, int depth, ref Walk walk)
    {
        walk.Visits++;
        if ((walk.Visits & CancellationCheckMask) == 0)
        {
            walk.CancellationToken.ThrowIfCancellationRequested();
        }

        if (depth > PdfLimits.MaxNesting)
        {
            // The writer refuses values this deep, so there is nothing more to find.
            return;
        }

        switch (value.Kind)
        {
            case PdfKind.Reference:
                {
                    Visit(value.AsReference().Number, ref walk);
                    break;
                }

            case PdfKind.Array:
                {
                    foreach (var item in value.AsArray()!.Items)
                    {
                        Scan(item, depth + 1, ref walk);
                    }

                    break;
                }

            case PdfKind.Dictionary or PdfKind.Stream:
                {
                    ScanEntries(value.AsDictionary()!, value.Kind == PdfKind.Stream, depth + 1, ref walk);
                    break;
                }

            default:
                {
                    // Numbers, names, strings and null hold no references.
                    break;
                }
        }
    }

    /// <summary>Scans a dictionary's values.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="isStream">Whether it is a stream dictionary, whose /Length the writer replaces.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="walk">The construction state.</param>
    private void ScanEntries(PdfDictionary dictionary, bool isStream, int depth, ref Walk walk)
    {
        for (var i = 0; i < dictionary.Count; i++)
        {
            if (!isStream || !dictionary.GetKeyAt(i).Is(KnownName.Length))
            {
                Scan(dictionary.GetValueAt(i), depth, ref walk);
            }
        }
    }

    /// <summary>Numbers an object the first time it is reached.</summary>
    /// <param name="number">The old object number.</param>
    /// <param name="walk">The construction state.</param>
    private void Visit(int number, ref Walk walk)
    {
        if ((uint)number >= (uint)Map.Length || number == 0 || Map[number] != 0)
        {
            return;
        }

        var value = walk.ReadObject(_store, number);
        if (value.IsNull)
        {
            Map[number] = -1;
            return;
        }

        walk.Add(number, value);
        Map[number] = walk.Count;
    }

    /// <summary>Operation-local graph construction with pooled array storage.</summary>
    private ref struct Walk : IDisposable
    {
        /// <summary>The temporary repairs while objects are collected.</summary>
        private PdfSaveRepairs _repairs;

        /// <summary>The old object numbers being collected.</summary>
        private PooledInts _numbers;

        /// <summary>The corresponding object values.</summary>
        private PooledValueStack _values;

        /// <summary>Initializes a new instance of the <see cref="Walk"/> struct.</summary>
        /// <param name="store">The document's objects.</param>
        /// <param name="cancellationToken">Cancels construction.</param>
        internal Walk(PdfObjectStore store, CancellationToken cancellationToken)
        {
            _numbers = default;
            _values = default;
            _repairs = new(store, true);
            CancellationToken = cancellationToken;
            Visits = 0;
        }

        /// <summary>Gets the token while objects are collected.</summary>
        internal readonly CancellationToken CancellationToken { get; }

        /// <summary>Gets or sets the number of values visited.</summary>
        internal int Visits { get; set; }

        /// <summary>Gets the number of collected objects.</summary>
        internal readonly int Count => _numbers.Count;

        /// <summary>Gets the old object numbers in traversal order.</summary>
        internal readonly ReadOnlySpan<int> Numbers => _numbers.Span;

        /// <summary>Gets the corresponding object values.</summary>
        internal readonly ReadOnlySpan<PdfValue> Values => _values.Span;

        /// <summary>Returns construction storage without recycling the completed snapshot arrays.</summary>
        public void Dispose()
        {
            _repairs.Dispose();
            _repairs = default;
            _numbers.Dispose();
            _values.Dispose();
        }

        /// <summary>Appends one object in traversal order.</summary>
        /// <param name="number">The old object number.</param>
        /// <param name="value">The repaired value.</param>
        internal void Add(int number, PdfValue value)
        {
            _numbers.Add(number);
            _values.Push(value);
        }

        /// <summary>Reads and repairs an object reached by the graph.</summary>
        /// <param name="store">The document's objects.</param>
        /// <param name="number">The old object number.</param>
        /// <returns>The repaired object, or null for a missing object.</returns>
        internal PdfValue ReadObject(PdfObjectStore store, int number)
        {
            var value = _repairs.TryGetNewObject(number, out var made) ? made : StoreReading.GetObject(store, new(number, 0));
            return value.IsNull ? default : _repairs.Fix(number, value);
        }
    }
}
