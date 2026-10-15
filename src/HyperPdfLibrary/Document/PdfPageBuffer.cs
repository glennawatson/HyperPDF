// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Collects the page entries of a read into the array the document keeps, with the distinct attributes the pages
/// inherit. The page-entry array starts at the size the page tree's <c>/Count</c> promises, so a well-formed tree hands it
/// over without a copy. Inherited attributes are built in pooled storage and copied into the retained snapshot.
/// </summary>
internal ref struct PdfPageBuffer : IDisposable
{
    /// <summary>The factor by which the array grows when the count was too small.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The most recent distinct attributes compared before a new one is added.</summary>
    private const int RecentInherits = 4;

    /// <summary>The distinct attributes the builder starts with room for.</summary>
    private const int InitialInherits = 4;

    /// <summary>The entries so far; the length is the capacity.</summary>
    private PdfPageEntry[] _entries;

    /// <summary>The rented inherited-attribute builder; the first entry is the empty set.</summary>
    private InheritedAttributes[] _inherits;

    /// <summary>The number of distinct inherited attributes.</summary>
    private int _inheritCount;

    /// <summary>Initializes a new instance of the <see cref="PdfPageBuffer"/> struct.</summary>
    /// <param name="capacity">The expected number of pages.</param>
    internal PdfPageBuffer(int capacity)
    {
        _entries = capacity == 0 ? [] : new PdfPageEntry[capacity];
        _inherits = ArrayPool<InheritedAttributes>.Shared.Rent(InitialInherits);
        _inherits[0] = default;
        _inheritCount = 1;
        Count = 0;
    }

    /// <summary>Gets the number of pages added.</summary>
    internal int Count { get; private set; }

    /// <summary>Returns inherited-attribute scratch storage without touching the page entries handed to the document.</summary>
    public void Dispose()
    {
        if (_inherits is { Length: > 0 } inherits)
        {
            ArrayPool<InheritedAttributes>.Shared.Return(inherits, true);
        }

        _inherits = [];
        _inheritCount = 0;
    }

    /// <summary>Adds a page.</summary>
    /// <param name="entry">The page's entry.</param>
    internal void Add(in PdfPageEntry entry)
    {
        if (Count == _entries.Length)
        {
            Array.Resize(ref _entries, Math.Max(1, _entries.Length * GrowthFactor));
        }

        _entries[Count] = entry;
        Count++;
    }

    /// <summary>Gets the position of some inherited attributes in the table, adding them when they are new.</summary>
    /// <param name="attributes">The attributes.</param>
    /// <param name="hint">A position likely to hold them, such as the parent node's.</param>
    /// <returns>The position.</returns>
    internal int Intern(in InheritedAttributes attributes, int hint)
    {
        if (_inherits[hint].Equals(attributes))
        {
            return hint;
        }

        var first = Math.Max(0, _inheritCount - RecentInherits);
        for (var i = _inheritCount - 1; i >= first; i--)
        {
            if (_inherits[i].Equals(attributes))
            {
                return i;
            }
        }

        if (_inheritCount == _inherits.Length)
        {
            var larger = ArrayPool<InheritedAttributes>.Shared.Rent(_inherits.Length * GrowthFactor);
            _inherits.AsSpan(0, _inheritCount).CopyTo(larger);
            ArrayPool<InheritedAttributes>.Shared.Return(_inherits, true);
            _inherits = larger;
        }

        _inherits[_inheritCount] = attributes;
        _inheritCount++;
        return _inheritCount - 1;
    }

    /// <summary>Gets the pages as a set.</summary>
    /// <param name="store">The store the pages are read from.</param>
    /// <returns>The set; it takes the page-entry array when full and owns a copy of the inherited attributes.</returns>
    internal readonly PdfPageSet ToSet(PdfObjectStore store) => new(
        store,
        Count == _entries.Length ? _entries : _entries.AsSpan(0, Count).ToArray(),
        _inherits.AsSpan(0, _inheritCount).ToArray());
}
