// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Collects the page entries of a read into the array the document keeps, with the distinct attributes the pages
/// inherit. The array starts at the size the page tree's <c>/Count</c> promises, so a well-formed tree is handed over
/// without a copy; a wrong count grows or trims it once.
/// </summary>
internal ref struct PdfPageBuffer
{
    /// <summary>The factor by which the array grows when the count was too small.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The most recent distinct attributes compared before a new one is added.</summary>
    private const int RecentInherits = 4;

    /// <summary>The distinct attributes the table starts with room for.</summary>
    private const int InitialInherits = 4;

    /// <summary>The entries so far; the length is the capacity.</summary>
    private PdfPageEntry[] _entries;

    /// <summary>The distinct inherited attributes so far; the first is the empty set.</summary>
    private InheritedAttributes[] _inherits;

    /// <summary>The number of distinct inherited attributes.</summary>
    private int _inheritCount;

    /// <summary>Initializes a new instance of the <see cref="PdfPageBuffer"/> struct.</summary>
    /// <param name="capacity">The expected number of pages.</param>
    internal PdfPageBuffer(int capacity)
    {
        _entries = capacity == 0 ? [] : new PdfPageEntry[capacity];
        _inherits = new InheritedAttributes[InitialInherits];
        _inheritCount = 1;
        Count = 0;
    }

    /// <summary>Gets the number of pages added.</summary>
    internal int Count { get; private set; }

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
            Array.Resize(ref _inherits, _inherits.Length * GrowthFactor);
        }

        _inherits[_inheritCount] = attributes;
        _inheritCount++;
        return _inheritCount - 1;
    }

    /// <summary>Gets the pages as a set.</summary>
    /// <param name="store">The store the pages are read from.</param>
    /// <returns>The set; it uses the buffer's own arrays when they are full.</returns>
    internal readonly PdfPageSet ToSet(PdfObjectStore store) => new(
        store,
        Count == _entries.Length ? _entries : _entries.AsSpan(0, Count).ToArray(),
        _inheritCount == _inherits.Length ? _inherits : _inherits.AsSpan(0, _inheritCount).ToArray());
}
