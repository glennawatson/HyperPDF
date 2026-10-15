// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The objects reachable from a trailer, numbered densely from 1 in breadth-first order so the catalog comes first.
/// A duplicate is never numbered: references to it take its original's number. Unreachable, free and missing objects
/// get no number. A graph is leased from a small pool with <see cref="Collect"/> and given back with <see cref="Return"/>.
/// </summary>
[DebuggerDisplay("OptimizerGraph: {Count} objects")]
internal sealed class OptimizerGraph
{
    /// <summary>The largest number table an idle graph keeps; a bigger one is dropped when the graph is given back.</summary>
    private const int MaxKeptMap = 1 << 20;

    /// <summary>The old number of each new number, less one.</summary>
    private readonly List<int> _numbers = [];

    /// <summary>The value of each new number, less one.</summary>
    private readonly List<PdfValue> _values = [];

    /// <summary>The objects read from.</summary>
    private PdfObjectStore? _store;

    /// <summary>The original each duplicate's references go to, by old number; zero when the object is no duplicate.</summary>
    private int[]? _aliases;

    /// <summary>1 once the graph has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="OptimizerGraph"/> class.</summary>
    /// <param name="resources">The registered service's reusable state.</param>
    private OptimizerGraph(OptimizerResources resources) => Resources = resources;

    /// <summary>Gets the registered service's reusable state.</summary>
    internal OptimizerResources Resources { get; }

    /// <summary>Gets the new number of each old number: zero for unreached objects, -1 for missing ones. It may be longer than the store; the extra entries are zero.</summary>
    internal int[] Map { get; private set; } = [];

    /// <summary>Gets the number of objects reached.</summary>
    internal int Count => _numbers.Count;

    /// <summary>Walks the objects reachable from a trailer's /Root and /Info, and /Encrypt when kept.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="keepEncrypt">Whether /Encrypt is kept.</param>
    /// <param name="aliases">The original of each duplicate, or <see langword="null"/>.</param>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>The graph, which the caller gives back with <see cref="Return"/>.</returns>
    /// <exception cref="PdfException">The trailer's /Root does not resolve to a dictionary.</exception>
    internal static OptimizerGraph Collect(PdfObjectStore store, bool keepEncrypt, int[]? aliases, OptimizerResources resources)
    {
        var graph = resources.Graphs.Take() ?? new(resources);
        Volatile.Write(ref graph._returned, 0);
        graph.Begin(store, aliases);
        try
        {
            graph.Walk(keepEncrypt);
        }
        catch
        {
            graph.Return();
            throw;
        }

        return graph;
    }

    /// <summary>Gives the graph back to the pool, emptied.</summary>
    internal void Return()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        _numbers.Clear();
        _values.Clear();
        _store = null;
        _aliases = null;
        if (Map.Length > MaxKeptMap)
        {
            Map = [];
        }

        _ = Resources.Graphs.Offer(this);
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

    /// <summary>Determines whether an old object number was reached, directly or as a duplicate.</summary>
    /// <param name="oldNumber">The old number.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsReached(int oldNumber) => (uint)oldNumber < (uint)Map.Length && Map[oldNumber] > 0;

    /// <summary>Points the graph at a store and clears its number table.</summary>
    /// <param name="store">The objects read from.</param>
    /// <param name="aliases">The original of each duplicate, or <see langword="null"/>.</param>
    private void Begin(PdfObjectStore store, int[]? aliases)
    {
        _store = store;
        _aliases = aliases;
        if (Map.Length < store.Size)
        {
            Map = new int[store.Size];
            return;
        }

        Array.Clear(Map);
    }

    /// <summary>Numbers the reachable objects.</summary>
    /// <param name="keepEncrypt">Whether /Encrypt is kept.</param>
    /// <exception cref="PdfException">The trailer's /Root does not resolve to a dictionary.</exception>
    private void Walk(bool keepEncrypt)
    {
        var store = _store!;
        var trailer = store.Trailer;
        if (StoreReading.Resolve(store, trailer.GetRaw(KnownName.Root)).AsDictionary() is null)
        {
            throw new PdfException(PdfError.Format, "The document's /Root does not resolve to a catalog.");
        }

        Scan(trailer.GetRaw(KnownName.Root), 0);
        Scan(trailer.GetRaw(KnownName.Info), 0);
        if (keepEncrypt)
        {
            Scan(trailer.GetRaw(KnownName.Encrypt), 0);
        }

        // The list grows while it is walked: a breadth-first queue.
        for (var i = 0; i < _values.Count; i++)
        {
            Scan(_values[i], 0);
        }
    }

    /// <summary>Finds the references in a value and numbers the objects they reach.</summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Scan(PdfValue value, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
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

    /// <summary>Numbers an object the first time it is reached, or gives a duplicate its original's number.</summary>
    /// <param name="number">The old object number.</param>
    private void Visit(int number)
    {
        if ((uint)number >= (uint)_store!.Size || number == 0 || Map[number] != 0)
        {
            return;
        }

        var original = _aliases is not null && _aliases[number] > 0 ? _aliases[number] : number;
        if (original != number)
        {
            Visit(original);
            Map[number] = Map[original];
            return;
        }

        var value = StoreReading.GetObject(_store, new(number, 0));
        if (value.IsNull)
        {
            Map[number] = -1;
            return;
        }

        _numbers.Add(number);
        _values.Add(value);
        Map[number] = _numbers.Count;
    }
}
