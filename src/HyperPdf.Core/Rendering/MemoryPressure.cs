// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Tells whether memory is short enough that optional work should wait.</summary>
public static class MemoryPressure
{
    /// <summary>The share of the tile budget, as a percentage, above which the tile cache counts as full.</summary>
    private const int FullPercent = 90;

    /// <summary>The percentage scale.</summary>
    private const int Hundred = 100;

    /// <summary>Gets whether the system's memory load is at or above the level the runtime calls high.</summary>
    /// <returns><see langword="true"/> when the system is short of memory.</returns>
    public static bool IsSystemHigh()
    {
        var info = GC.GetGCMemoryInfo();
        return info.HighMemoryLoadThresholdBytes > 0 && info.MemoryLoadBytes >= info.HighMemoryLoadThresholdBytes;
    }

    /// <summary>Gets whether the tile cache is close to its budget.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns><see langword="true"/> when the cache holds nine tenths of its budget or more.</returns>
    public static bool IsCacheFull(TileCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return cache.CurrentBytes * Hundred >= cache.BudgetBytes * FullPercent;
    }
}
