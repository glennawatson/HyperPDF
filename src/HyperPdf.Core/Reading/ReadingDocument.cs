// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>
/// A document's pages in reading order, worked out when first asked for and kept. Running headers and footers are
/// found by sampling pages across the document for margin lines that repeat. Safe to use from any thread.
/// </summary>
[DebuggerDisplay("ReadingDocument: {_sizes.Length} pages")]
public sealed class ReadingDocument
{
    /// <summary>The most pages sampled for running headers and footers.</summary>
    private const int MaxSamples = 16;

    /// <summary>The sentinel before any page character can extend a run.</summary>
    private const int NoPreviousCharacter = -2;

    /// <summary>The separator placed between blocks in flattened text, which also ends a sentence.</summary>
    private const string BlockSeparator = "\n\n";

    /// <summary>Gets the character source, which may be closed and reopened while the document is in the background.</summary>
    private readonly Func<ReadingSources?> _source;

    /// <summary>The page sizes.</summary>
    private readonly PageSize[] _sizes;

    /// <summary>The pages worked out so far.</summary>
    private readonly Dictionary<int, ReadingPage> _pages = [];

    /// <summary>Guards the caches.</summary>
    private readonly Lock _gate = new();

    /// <summary>The repeated margin signatures, once sampled.</summary>
    private HashSet<string>? _repeatedMargins;

    /// <summary>Initializes a new instance of the <see cref="ReadingDocument"/> class.</summary>
    /// <param name="source">The document's characters.</param>
    /// <param name="sizes">The page sizes in points.</param>
    public ReadingDocument(ITextLayoutSource source, PageSize[] sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(source);
        _source = () => new ReadingSources(source, source as ITaggedStructureSource);
        _sizes = sizes;
    }

    /// <summary>Initializes a new instance of the <see cref="ReadingDocument"/> class for a document that may be reopened.</summary>
    /// <param name="source">Gets the document's characters, or <see langword="null"/> when it cannot be opened.</param>
    /// <param name="sizes">The page sizes in points.</param>
    public ReadingDocument(Func<ITextLayoutSource?> source, PageSize[] sizes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sizes);
        _source = () => source() is { } layout ? new ReadingSources(layout, layout as ITaggedStructureSource) : null;
        _sizes = sizes;
    }

    /// <summary>Initializes a new instance of the <see cref="ReadingDocument"/> class with features from one opened document.</summary>
    /// <param name="source">Gets the character layout and logical structure together.</param>
    /// <param name="sizes">The page sizes in points.</param>
    private ReadingDocument(Func<ReadingSources?> source, PageSize[] sizes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sizes);
        _source = source;
        _sizes = sizes;
    }

    /// <summary>Gets the number of pages.</summary>
    public int PageCount => _sizes.Length;

    /// <summary>Creates reading order from features resolved together on one opened document.</summary>
    /// <param name="source">Gets the character layout and logical structure together.</param>
    /// <param name="sizes">The page sizes in points.</param>
    /// <returns>The document's reading order.</returns>
    public static ReadingDocument Create(Func<ReadingSources?> source, PageSize[] sizes) => new(source, sizes);

    /// <summary>Joins a page's blocks into one text for reading aloud, each block ending a sentence.</summary>
    /// <param name="page">The page.</param>
    /// <param name="map">Receives the page character index of each character of the text; -1 for added spaces.</param>
    /// <returns>The text.</returns>
    public static string Flatten(ReadingPage page, out int[] map)
    {
        ArgumentNullException.ThrowIfNull(page);
        var length = 0;
        foreach (var block in page.Blocks)
        {
            length += block.Text.Length + BlockSeparator.Length;
        }

        var text = new char[length];
        map = new int[length];
        var at = 0;
        foreach (var block in page.Blocks)
        {
            block.Text.CopyTo(text.AsSpan(at));
            block.CharIndices.CopyTo(map, at);
            at += block.Text.Length;
            foreach (var separator in BlockSeparator)
            {
                text[at] = separator;
                map[at] = -1;
                at++;
            }
        }

        return new(text);
    }

    /// <summary>Gets the runs of page characters behind a range of flattened text, for highlighting it on the page.</summary>
    /// <param name="map">The map from <see cref="Flatten"/>.</param>
    /// <param name="start">The first character of the range.</param>
    /// <param name="length">The range's length.</param>
    /// <param name="runs">Receives each run of consecutive page characters as a start and count.</param>
    public static void GetRuns(int[] map, int start, int length, List<ReadingCharacterRun> runs)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(runs);
        var runStart = -1;
        var previous = NoPreviousCharacter;
        for (var i = start; i < start + length && i < map.Length; i++)
        {
            var index = map[i];
            if (index < 0)
            {
                continue;
            }

            if (index != previous + 1 && runStart >= 0)
            {
                runs.Add(new(runStart, previous - runStart + 1));
                runStart = -1;
            }

            runStart = runStart < 0 ? index : runStart;
            previous = index;
        }

        if (runStart >= 0)
        {
            runs.Add(new(runStart, previous - runStart + 1));
        }
    }

    /// <summary>Gets a page in reading order.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The page's blocks.</returns>
    public ReadingPage GetPage(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, _sizes.Length);
        lock (_gate)
        {
            if (_pages.TryGetValue(pageIndex, out var cached))
            {
                return cached;
            }

            if (_source() is not { Characters: { } source } sources)
            {
                return new(pageIndex, []);
            }

            var characters = new List<PageCharacter>();
            source.GetCharacters(pageIndex, characters);
            var tagged = new List<TaggedBlock>();
            var page = sources.Structure is { } structure && structure.GetTaggedBlocks(pageIndex, tagged)
                ? ReadingOrder.FromStructure(pageIndex, characters, tagged)
                : AnalyzePage(pageIndex, source, characters);
            _pages[pageIndex] = page;
            return page;
        }
    }

    /// <summary>Forgets the worked out pages, for example after text is recognised or the file changes.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _pages.Clear();
            _repeatedMargins = null;
        }
    }

    /// <summary>Builds a page when it has no usable tagged structure.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="source">The character source.</param>
    /// <param name="characters">The page characters.</param>
    /// <returns>The analyzed page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadingPage AnalyzePage(int pageIndex, ITextLayoutSource source, List<PageCharacter> characters) =>
        ReadingOrder.Analyze(pageIndex, _sizes[pageIndex], characters, GetRepeatedMargins(source));

    /// <summary>Samples pages spread across the document for repeated header and footer lines. Callers hold the gate.</summary>
    /// <param name="source">The character source.</param>
    /// <returns>The repeated signatures.</returns>
    private HashSet<string> GetRepeatedMargins(ITextLayoutSource source)
    {
        if (_repeatedMargins is not null)
        {
            return _repeatedMargins;
        }

        var samples = Math.Min(MaxSamples, _sizes.Length);
        var pages = new List<List<string>>(samples);
        var characters = new List<PageCharacter>();
        for (var i = 0; i < samples; i++)
        {
            var page = (int)((long)i * _sizes.Length / samples);
            characters.Clear();
            source.GetCharacters(page, characters);
            var signatures = new List<string>();
            ReadingOrder.CollectMarginSignatures(_sizes[page], characters, signatures);
            pages.Add(signatures);
        }

        _repeatedMargins = ReadingOrder.FindRepeatedMargins(pages);
        return _repeatedMargins;
    }
}
