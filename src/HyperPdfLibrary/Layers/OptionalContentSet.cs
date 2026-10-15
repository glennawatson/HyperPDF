// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// The layers read from a document under one configuration, and their state. The per-layer sets are bit sets whose bit
/// <c>n</c> belongs to the layer with the n-th smallest object number in <see cref="SortedIds"/>.
/// </summary>
/// <param name="Layers">The layers in document order.</param>
/// <param name="SortedIds">The object numbers of the layers, ascending, which gives each layer its bit.</param>
/// <param name="Hidden">The groups hidden by default.</param>
/// <param name="Current">The groups hidden now; starts as a copy of <paramref name="Hidden"/> and changes as layers are shown and hidden.</param>
/// <param name="NonView">The groups that are not for viewing, and so are always shown.</param>
/// <param name="RadioGroups">The sets of groups of which only one is shown.</param>
/// <param name="PrintHidden">The groups hidden when printing.</param>
/// <param name="ExportHidden">The groups hidden when the document is saved to a format without optional content.</param>
/// <param name="LockedIds">The object numbers of the locked groups, ascending.</param>
/// <param name="ZoomRanges">The zoom classes in which each group is shown, by bit; empty when no group is zoom-managed.</param>
/// <param name="ZoomLimits">The distinct magnifications at which a zoom-managed group changes state, ascending.</param>
/// <param name="Tree">The layer order as a tree.</param>
/// <param name="ListMode">Which layers of the order a panel lists.</param>
/// <param name="Configurations">The configurations.</param>
/// <param name="ActiveConfiguration">The configuration in use.</param>
[DebuggerDisplay("OptionalContentSet: {Layers.Length} layers")]
internal sealed record OptionalContentSet(
    PdfLayer[] Layers,
    int[] SortedIds,
    int[] Hidden,
    int[] Current,
    int[] NonView,
    int[][] RadioGroups,
    int[] PrintHidden,
    int[] ExportHidden,
    int[] LockedIds,
    ZoomClassRange[] ZoomRanges,
    double[] ZoomLimits,
    PdfLayerNode[] Tree,
    PdfLayerListMode ListMode,
    PdfLayerConfiguration[] Configurations,
    int ActiveConfiguration)
{
    /// <summary>The bits in one word of a bit set.</summary>
    private const int WordBits = 32;

    /// <summary>The shift that turns a bit number into its word number.</summary>
    private const int WordShift = 5;

    /// <summary>The mask of a bit number within its word.</summary>
    private const int BitMask = WordBits - 1;

    /// <summary>Gets the set of a document without layers.</summary>
    internal static OptionalContentSet Empty { get; } = new([], [], [], [], [], [], [], [], [], [], [], [], PdfLayerListMode.AllPages, [], PdfLayerConfiguration.DefaultIndex);

    /// <summary>Gets the number of words a bit set for a count of layers needs.</summary>
    /// <param name="layers">The number of layers.</param>
    /// <returns>The number of words.</returns>
    internal static int WordCount(int layers) => (layers + BitMask) >> WordShift;

    /// <summary>Determines whether a bit is set.</summary>
    /// <param name="words">The bit set.</param>
    /// <param name="bit">The bit number.</param>
    /// <returns><see langword="true"/> when set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Test(int[] words, int bit) => (Volatile.Read(ref words[bit >> WordShift]) & (1 << (bit & BitMask))) != 0;

    /// <summary>Sets a bit without synchronisation, while a bit set is being built.</summary>
    /// <param name="words">The bit set.</param>
    /// <param name="bit">The bit number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Build(int[] words, int bit) => words[bit >> WordShift] |= 1 << (bit & BitMask);

    /// <summary>Sets a bit so a thread that reads the set sees it whole.</summary>
    /// <param name="words">The bit set.</param>
    /// <param name="bit">The bit number.</param>
    /// <returns><see langword="true"/> when the bit was clear before.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Raise(int[] words, int bit)
    {
        var mask = 1 << (bit & BitMask);
        return (Interlocked.Or(ref words[bit >> WordShift], mask) & mask) == 0;
    }

    /// <summary>Clears a bit so a thread that reads the set sees it whole.</summary>
    /// <param name="words">The bit set.</param>
    /// <param name="bit">The bit number.</param>
    /// <returns><see langword="true"/> when the bit was set before.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Lower(int[] words, int bit)
    {
        var mask = 1 << (bit & BitMask);
        return (Interlocked.And(ref words[bit >> WordShift], ~mask) & mask) != 0;
    }

    /// <summary>Finds the bit of a layer.</summary>
    /// <param name="id">The group's object number.</param>
    /// <returns>The bit number, or -1 when the document has no such layer.</returns>
    internal int IndexOf(int id)
    {
        var found = Array.BinarySearch(SortedIds, id);
        return found < 0 ? -1 : found;
    }

    /// <summary>Determines whether the configuration locks a group.</summary>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when locked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsLocked(int id) => Array.BinarySearch(LockedIds, id) >= 0;

    /// <summary>Determines whether the configuration hides a group by default.</summary>
    /// <param name="id">The group's object number.</param>
    /// <returns><see langword="true"/> when hidden by default.</returns>
    internal bool IsHiddenByDefault(int id) => IndexOf(id) is var bit && bit >= 0 && Test(Hidden, bit);
}
