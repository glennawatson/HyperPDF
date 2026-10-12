// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>Collects code ranges while a CMap is parsed, then sorts them into a <see cref="CodeRangeMap"/>.</summary>
[DebuggerDisplay("CodeRangeMapBuilder: {Count} ranges")]
internal sealed class CodeRangeMapBuilder
{
    /// <summary>The ranges in the order they were added.</summary>
    private readonly List<CodeRangeEntry> _ranges = [];

    /// <summary>Gets the number of ranges added.</summary>
    internal int Count => _ranges.Count;

    /// <summary>Gets the ranges in the order they were added, until <see cref="Build"/> sorts them.</summary>
    internal IReadOnlyList<CodeRangeEntry> Entries => _ranges;

    /// <summary>Gets the largest value any added range gives its last code, or zero when none was added.</summary>
    internal long MaxValue { get; private set; }

    /// <summary>Adds a range.</summary>
    /// <param name="low">The first code.</param>
    /// <param name="high">The last code.</param>
    /// <param name="value">The value of the first code.</param>
    internal void Add(uint low, uint high, int value)
    {
        if (high < low)
        {
            return;
        }

        _ranges.Add(new(low, high, value, _ranges.Count));
        MaxValue = Math.Max(MaxValue, value + (long)(high - low));
    }

    /// <summary>Builds the map.</summary>
    /// <param name="mergeSequences">Whether adjacent ranges whose values continue each other are joined, as for CIDs.</param>
    /// <returns>The map.</returns>
    internal CodeRangeMap Build(bool mergeSequences)
    {
        if (_ranges.Count == 0)
        {
            return CodeRangeMap.Empty;
        }

        _ranges.Sort(static (a, b) => a.Low != b.Low ? a.Low.CompareTo(b.Low) : a.Order.CompareTo(b.Order));
        var lows = new List<uint>(_ranges.Count);
        var highs = new List<uint>(_ranges.Count);
        var values = new List<int>(_ranges.Count);
        foreach (var (low, high, value, _) in _ranges)
        {
            var last = lows.Count - 1;
            if (mergeSequences && last >= 0 && highs[last] + 1 == low && values[last] + (long)(low - lows[last]) == value)
            {
                highs[last] = high;
                continue;
            }

            lows.Add(low);
            highs.Add(high);
            values.Add(value);
        }

        return new([.. lows], [.. highs], [.. values]);
    }
}
