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

    /// <summary>The objects read from.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The token for this graph walk.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>Makes the catalog, page tree and pages conform as they are numbered.</summary>
    private readonly PdfSaveRepairs _repairs;

    /// <summary>The old number of each new number, less one.</summary>
    private readonly List<int> _numbers = [];

    /// <summary>The value of each new number, less one.</summary>
    private readonly List<PdfValue> _values = [];

    /// <summary>The values visited since the walk began.</summary>
    private int _visits;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectGraph"/> class.</summary>
    /// <param name="store">The objects read from.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private PdfObjectGraph(PdfObjectStore store, CancellationToken cancellationToken)
    {
        _store = store;
        _cancellationToken = cancellationToken;
        _repairs = new(store, true);

        // One extra slot for a page tree root the repairs may have to make.
        Map = new int[store.Size + 1];
    }

    /// <summary>Gets the new number of each old number: zero for unreached objects, -1 for missing ones.</summary>
    internal int[] Map { get; }

    /// <summary>Gets the name table the objects' names come from.</summary>
    internal PdfNameTable Names => _store.Names;

    /// <summary>Gets the number of objects reached.</summary>
    internal int Count => _numbers.Count;

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
        var graph = new PdfObjectGraph(store, cancellationToken);
        var trailer = store.Trailer;
        graph.Scan(trailer.GetRaw(KnownName.Root), 0);
        if (StoreReading.Resolve(store, trailer.GetRaw(KnownName.Root)).AsDictionary() is null)
        {
            throw new PdfException(PdfError.Format, "The document's /Root does not resolve to a catalog.");
        }

        graph.Scan(trailer.GetRaw(KnownName.Info), 0);
        if (keepEncrypt)
        {
            graph.Scan(trailer.GetRaw(KnownName.Encrypt), 0);
        }

        // The list grows while it is walked: a breadth-first queue.
        var walked = graph.Drain(0);
        if (keepUnreferenced)
        {
            graph.AddUnreferenced(keepEncrypt ? 0 : PdfXrefWriter.EncryptNumber(trailer), walked);
        }

        return graph;
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
    /// <returns>The index after the last object scanned.</returns>
    private int Drain(int next)
    {
        for (; next < _values.Count; next++)
        {
            Scan(_values[next], 0);
        }

        return next;
    }

    /// <summary>Numbers the objects in use that no reference reached, with everything each of them reaches.</summary>
    /// <param name="skippedEncrypt">The number of an /Encrypt dictionary that is not kept, or zero.</param>
    /// <param name="walked">The index after the last object already scanned.</param>
    private void AddUnreferenced(int skippedEncrypt, int walked)
    {
        for (var number = 1; number < _store.Size; number++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (Map[number] != 0 || number == skippedEncrypt || IsFileStructure(StoreReading.GetObject(_store, new(number, 0))))
            {
                continue;
            }

            Visit(number);
            walked = Drain(walked);
        }
    }

    /// <summary>Finds the references in a value and numbers the objects they reach.</summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Scan(PdfValue value, int depth)
    {
        _visits++;
        if ((_visits & CancellationCheckMask) == 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
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
                    Visit(value.AsReference().Number);
                    break;
                }

            case PdfKind.Array:
                {
                    foreach (var item in value.AsArray()!.Items)
                    {
                        Scan(item, depth + 1);
                    }

                    break;
                }

            case PdfKind.Dictionary or PdfKind.Stream:
                {
                    ScanEntries(value.AsDictionary()!, value.Kind == PdfKind.Stream, depth + 1);
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
    private void ScanEntries(PdfDictionary dictionary, bool isStream, int depth)
    {
        for (var i = 0; i < dictionary.Count; i++)
        {
            if (!isStream || !dictionary.GetKeyAt(i).Is(KnownName.Length))
            {
                Scan(dictionary.GetValueAt(i), depth);
            }
        }
    }

    /// <summary>Numbers an object the first time it is reached.</summary>
    /// <param name="number">The old object number.</param>
    private void Visit(int number)
    {
        if ((uint)number >= (uint)Map.Length || number == 0 || Map[number] != 0)
        {
            return;
        }

        var value = _repairs.TryGetNewObject(number, out var made) ? made : StoreReading.GetObject(_store, new(number, 0));
        if (value.IsNull)
        {
            Map[number] = -1;
            return;
        }

        _numbers.Add(number);
        _values.Add(_repairs.Fix(number, value));
        Map[number] = _numbers.Count;
    }
}
