// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Loads the bytes an operation will read into a file source's cache with async I/O, level by level: the objects one level
/// down are read from the cache the level before filled, so the synchronous core that follows never waits on the file.
/// Sources that are in memory or mapped skip all of it.
/// </summary>
internal static class PdfPrefetcher
{
    /// <summary>The bytes read from each end of a file too big for the cache.</summary>
    private const int EndBytes = 256 * 1024;

    /// <summary>How many times the load-ahead budget a file may be to be loaded whole: the budget is half the cache.</summary>
    private const int WholeFileFactor = 2;

    /// <summary>The most levels followed from a page.</summary>
    private const int PageLevels = 6;

    /// <summary>The most levels followed down a page tree.</summary>
    private const int TreeLevels = 12;

    /// <summary>The most levels followed along an outline.</summary>
    private const int OutlineLevels = 256;

    /// <summary>Loads a file's ends, or all of it when it fits the cache, so opening finds the header, trailer and cross-reference data cached.</summary>
    /// <param name="source">The file.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the bytes are cached.</returns>
    internal static ValueTask PrefetchFileAsync(PdfByteSource source, CancellationToken cancellationToken)
    {
        if (!source.NeedsPrefetch)
        {
            return ValueTask.CompletedTask;
        }

        return source.Length <= source.PrefetchBudget * WholeFileFactor ? source.PrefetchAsync(0, source.Length, cancellationToken) : PrefetchEndsAsync(source, cancellationToken);
    }

    /// <summary>Loads the objects a starting dictionary leads to.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="start">The page, the /Pages node or the /Outlines dictionary.</param>
    /// <param name="kind">Which references to follow.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the levels are cached.</returns>
    internal static ValueTask PrefetchAsync(PdfObjectStore objects, PdfDictionary start, PdfPrefetchKind kind, CancellationToken cancellationToken)
    {
        var source = objects.Source;
        if (!source.NeedsPrefetch)
        {
            return ValueTask.CompletedTask;
        }

        // While nothing has been dropped from the cache since the last walk, what it loaded is still there.
        var mark = objects.PrefetchMarks.GetValue(start, static _ => new());
        var generation = source.CacheGeneration;
        return mark.IsLoadedAt(kind, generation) ? ValueTask.CompletedTask : WalkAsync(objects, start, kind, mark, generation, cancellationToken);
    }

    /// <summary>Walks the objects a dictionary leads to, loading each level.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="start">The starting dictionary.</param>
    /// <param name="kind">Which references to follow.</param>
    /// <param name="mark">Receives the generation the walk started at once it completes.</param>
    /// <param name="generation">The cache generation when the walk started.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the levels are cached.</returns>
    private static async ValueTask WalkAsync(PdfObjectStore objects, PdfDictionary start, PdfPrefetchKind kind, PdfPrefetchMark mark, long generation, CancellationToken cancellationToken)
    {
        var source = objects.Source;
        var walk = Walk.Rent(objects, kind);
        try
        {
            walk.Scan(objects.Names, start, 0);
            walk.Promote();
            var budget = source.PrefetchBudget;
            for (var level = 0; level < LevelsFor(kind) && walk.Frontier.Count > 0 && budget > 0; level++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget -= await LoadAsync(objects, walk, budget, cancellationToken).ConfigureAwait(false);
                walk.Advance(objects);
            }

            mark.SetLoadedAt(kind, generation);
        }
        finally
        {
            walk.Release(objects);
        }
    }

    /// <summary>Gets the levels followed for a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The level count.</returns>
    private static int LevelsFor(PdfPrefetchKind kind) => kind switch
    {
        PdfPrefetchKind.PageTree => TreeLevels,
        PdfPrefetchKind.Outline => OutlineLevels,
        _ => PageLevels,
    };

    /// <summary>Loads the first and last part of a file.</summary>
    /// <param name="source">The file.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the bytes are cached.</returns>
    private static async ValueTask PrefetchEndsAsync(PdfByteSource source, CancellationToken cancellationToken)
    {
        await source.PrefetchAsync(0, EndBytes, cancellationToken).ConfigureAwait(false);
        await source.PrefetchAsync(source.Length - EndBytes, EndBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the byte ranges of the objects on the walk's frontier.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="budget">The most bytes to load.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The number of bytes loaded.</returns>
    private static async ValueTask<long> LoadAsync(PdfObjectStore objects, Walk walk, long budget, CancellationToken cancellationToken)
    {
        var ranges = walk.Ranges(objects);
        var loaded = 0L;
        foreach (var (offset, length) in ranges)
        {
            if (loaded + length > budget)
            {
                break;
            }

            await objects.Source.PrefetchAsync(offset, length, cancellationToken).ConfigureAwait(false);
            loaded += length;
        }

        return loaded;
    }

    /// <summary>The references found so far, and the level being read.</summary>
    private sealed class Walk
    {
        /// <summary>The longest object, in bytes, that is loaded ahead; longer ones are read straight from the file.</summary>
        private const long MaxExtent = 1L << 20;

        /// <summary>The gap, in bytes, between two objects that still loads as one read.</summary>
        private const long MergeGap = 16 * 1024;

        /// <summary>The deepest direct dictionary or array searched for references.</summary>
        private const int MaxNesting = 8;

        /// <summary>The largest collection capacity retained for another walk.</summary>
        private const int MaxKeptObjects = 4096;

        /// <summary>The objects already queued.</summary>
        private readonly HashSet<int> _seen = [];

        /// <summary>The objects found in the next level.</summary>
        private readonly List<PdfObjectId> _next = [];

        /// <summary>The byte ranges reused between levels.</summary>
        private readonly List<PdfByteRange> _ranges = [];

        /// <summary>Which references to follow during this operation.</summary>
        private PdfPrefetchKind _kind;

        /// <summary>Gets the objects whose bytes are loaded this level.</summary>
        internal List<PdfObjectId> Frontier { get; } = [];

        /// <summary>Gets known keys whose references lead outside the page content.</summary>
        private static ReadOnlySpan<int> PageBoundaryIds =>
        [
            (int)KnownName.Parent, (int)KnownName.P, (int)KnownName.Prev, (int)KnownName.Next,
            (int)KnownName.Dest, (int)KnownName.D, (int)KnownName.B, (int)KnownName.Popup,
            (int)KnownName.IRT, (int)KnownName.Metadata, (int)KnownName.StructParent, (int)KnownName.AF,
        ];

        /// <summary>Takes an empty walk and selects the references to follow.</summary>
        /// <param name="objects">The document's objects.</param>
        /// <param name="kind">Which references to follow.</param>
        /// <returns>The walk owned by this operation until released.</returns>
        internal static Walk Rent(PdfObjectStore objects, PdfPrefetchKind kind)
        {
            var walk = objects.ScratchPools.Slots<Walk>().Take() ?? new();
            walk._kind = kind;
            return walk;
        }

        /// <summary>Clears the operation's state and retains its buffers for another walk.</summary>
        /// <param name="objects">The document's objects.</param>
        internal void Release(PdfObjectStore objects)
        {
            var keep = _seen.EnsureCapacity(0) <= MaxKeptObjects
                && _next.Capacity <= MaxKeptObjects
                && _ranges.Capacity <= MaxKeptObjects
                && Frontier.Capacity <= MaxKeptObjects;
            _seen.Clear();
            _next.Clear();
            _ranges.Clear();
            Frontier.Clear();
            if (keep)
            {
                _ = objects.ScratchPools.Slots<Walk>().Offer(this);
            }
        }

        /// <summary>Finds the references inside a dictionary or array value.</summary>
        /// <param name="names">The document's names.</param>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="nesting">How deep the search is.</param>
        internal void Scan(PdfNameTable names, PdfDictionary dictionary, int nesting)
        {
            for (var i = 0; i < dictionary.Count; i++)
            {
                if (Follows(names, dictionary.GetKeyAt(i)))
                {
                    ScanValue(names, dictionary.GetValueAt(i), nesting);
                }
            }
        }

        /// <summary>Moves to the next level: reads the objects just loaded and queues the references inside them.</summary>
        /// <param name="objects">The document's objects.</param>
        internal void Advance(PdfObjectStore objects)
        {
            foreach (var id in Frontier)
            {
                if (ReadChildren(objects, id) is { } dictionary)
                {
                    Scan(objects.Names, dictionary, 1);
                }
            }

            Promote();
        }

        /// <summary>Makes the references found so far the frontier.</summary>
        internal void Promote()
        {
            Frontier.Clear();
            Frontier.AddRange(_next);
            _next.Clear();
        }

        /// <summary>Gets the merged byte ranges of the frontier's objects in file order.</summary>
        /// <param name="objects">The document's objects.</param>
        /// <returns>The ranges.</returns>
        internal List<PdfByteRange> Ranges(PdfObjectStore objects)
        {
            var found = _ranges;
            found.Clear();
            foreach (var id in Frontier)
            {
                if (StoreExtents.TryGetExtent(objects, id.Number, out var offset, out var length) && length <= MaxExtent)
                {
                    found.Add(new(offset, length));
                }
            }

            found.Sort(static (a, b) => a.Offset.CompareTo(b.Offset));
            return Merge(found);
        }

        /// <summary>Joins ranges that overlap or lie close together.</summary>
        /// <param name="sorted">The ranges in file order.</param>
        /// <returns>The merged ranges.</returns>
        private static List<PdfByteRange> Merge(List<PdfByteRange> sorted)
        {
            var count = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                var range = sorted[i];
                if (count > 0 && range.Offset <= sorted[count - 1].Offset + sorted[count - 1].Length + MergeGap)
                {
                    var last = sorted[count - 1];
                    sorted[count - 1] = new(last.Offset, Math.Max(last.Length, range.Offset + range.Length - last.Offset));
                }
                else
                {
                    sorted[count] = range;
                    count++;
                }
            }

            sorted.RemoveRange(count, sorted.Count - count);
            return sorted;
        }

        /// <summary>Recognises page references that lead outside the content being prefetched.</summary>
        /// <param name="names">The document's names.</param>
        /// <param name="key">The key.</param>
        /// <returns>Whether the reference should be excluded.</returns>
        private static bool IsPageBoundary(PdfNameTable names, PdfName key) =>
            key.IsKnown ? PageBoundaryIds.Contains(key.Id) : names.NameEquals(key, "Thumb"u8) || names.NameEquals(key, "PieceInfo"u8);

        /// <summary>Determines whether a key's value is searched for references.</summary>
        /// <param name="names">The document's names.</param>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        private bool Follows(PdfNameTable names, PdfName key) => _kind switch
        {
            PdfPrefetchKind.PageTree => key.Is(KnownName.Kids),
            PdfPrefetchKind.Outline => key.ToKnownName() is KnownName.First or KnownName.Next,
            PdfPrefetchKind.Separation => key.ToKnownName() is KnownName.Pages or KnownName.ColorSpace
                || names.NameEquals(key, "SeparationInfo"u8) || names.NameEquals(key, "DeviceColorant"u8),
            _ => !IsPageBoundary(names, key),
        };

        /// <summary>Searches a value for references, queueing the ones not seen before.</summary>
        /// <param name="names">The document's names.</param>
        /// <param name="value">The value.</param>
        /// <param name="nesting">How deep the search is.</param>
        private void ScanValue(PdfNameTable names, PdfValue value, int nesting)
        {
            if (value.IsReference)
            {
                var id = value.AsReference();
                if (id.IsValid && _seen.Add(id.Number))
                {
                    _next.Add(id);
                }

                return;
            }

            if (nesting >= MaxNesting)
            {
                return;
            }

            if (value.AsArray() is { } array)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    ScanValue(names, array.GetRaw(i), nesting + 1);
                }
            }
            else if (value.Kind == PdfKind.Dictionary && value.AsDictionary() is { } dictionary)
            {
                Scan(names, dictionary, nesting + 1);
            }
        }

        /// <summary>Reads an object loaded in the last level, returning its dictionary when its references should be followed.</summary>
        /// <param name="objects">The document's objects.</param>
        /// <param name="id">The object.</param>
        /// <returns>The dictionary, or <see langword="null"/> when it holds nothing to follow.</returns>
        private PdfDictionary? ReadChildren(PdfObjectStore objects, PdfObjectId id)
        {
            var dictionary = StoreReading.GetObject(objects, id).AsDictionary();
            if (dictionary is null || _kind is not (PdfPrefetchKind.Page or PdfPrefetchKind.Separation))
            {
                return dictionary;
            }

            // Another page (for a separation, one listed in /Pages), or the tree above this one, is reached through a destination or a stray reference: do not walk it.
            return dictionary.IsName(KnownName.Type, KnownName.Page) || dictionary.IsName(KnownName.Type, KnownName.Pages) ? null : dictionary;
        }
    }
}
