// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// The running headers and footers of a document: text that repeats in the top or the bottom margin of at least half
/// the pages that have text. Texts compare with each run of digits replaced by one mark, so "Page 3" and "Page 4" match.
/// Layout inference uses it to mark that text as pagination artifacts, outside the reading order.
/// </summary>
[DebuggerDisplay("PdfPaginationProfile: {_headers.Count} headers, {_footers.Count} footers")]
public sealed class PdfPaginationProfile
{
    /// <summary>The fewest pages a text must repeat on.</summary>
    private const int MinPages = 2;

    /// <summary>The share of the pages with text that a text must repeat on.</summary>
    private const float MinShare = 0.5F;

    /// <summary>The compared texts that repeat in the top margin.</summary>
    private readonly HashSet<string> _headers;

    /// <summary>The compared texts that repeat in the bottom margin.</summary>
    private readonly HashSet<string> _footers;

    /// <summary>Initializes a new instance of the <see cref="PdfPaginationProfile"/> class.</summary>
    /// <param name="headers">The compared texts that repeat in the top margin.</param>
    /// <param name="footers">The compared texts that repeat in the bottom margin.</param>
    private PdfPaginationProfile(HashSet<string> headers, HashSet<string> footers)
    {
        _headers = headers;
        _footers = footers;
    }

    /// <summary>Gets a profile that finds nothing.</summary>
    public static PdfPaginationProfile Empty { get; } = new([], []);

    /// <summary>Gets the number of repeating texts found.</summary>
    public int Count => _headers.Count + _footers.Count;

    /// <summary>Reads the pages of a document for text that repeats in their margins.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Stops the search between pages.</param>
    /// <returns>The profile; empty for a document of fewer than two pages with text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfPaginationProfile Find(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Scan(document, cancellationToken);
    }

    /// <summary>Reads the pages of a document for text that repeats in their margins, loading each page's objects first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Stops the search between pages.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfPaginationProfile> FindAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var i = 0; i < document.PageCount; i++)
        {
            await PdfPrefetcher.PrefetchAsync(document.Objects, PdfDocumentPages.GetPage(document, i).Dictionary, PdfPrefetchKind.Page, cancellationToken).ConfigureAwait(false);
        }

        return Scan(document, cancellationToken);
    }

    /// <summary>Determines whether a compared text repeats in a margin.</summary>
    /// <param name="key">The compared text.</param>
    /// <param name="top">Whether to look in the top margin rather than the bottom one.</param>
    /// <returns><see langword="true"/> when the text is a running header or footer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(ReadOnlySpan<char> key, bool top) =>
        (top ? _headers : _footers).GetAlternateLookup<ReadOnlySpan<char>>().Contains(key);

    /// <summary>Counts the margin texts of the pages that are loaded.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Stops the search between pages.</param>
    /// <returns>The profile.</returns>
    private static PdfPaginationProfile Scan(PdfDocument document, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        var footers = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var withText = 0;
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = PdfDocumentTagged.GetMarkedContent(document, i);
            if (content.GlyphCount == 0)
            {
                continue;
            }

            withText++;
            CountMargins(content, headers, footers, seen);
        }

        return withText < MinPages ? Empty : new(Repeating(headers, withText), Repeating(footers, withText));
    }

    /// <summary>Picks the texts that repeat enough.</summary>
    /// <param name="counts">How many pages each text is on.</param>
    /// <param name="withText">The pages with text.</param>
    /// <returns>The repeating texts.</returns>
    private static HashSet<string> Repeating(Dictionary<string, int> counts, int withText)
    {
        var repeating = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, pages) in counts)
        {
            if (pages >= MinPages && pages >= withText * MinShare)
            {
                _ = repeating.Add(key);
            }
        }

        return repeating;
    }

    /// <summary>Counts the margin lines of a page, once for each text however often it appears.</summary>
    /// <param name="content">The page.</param>
    /// <param name="headers">Receives the top margin texts.</param>
    /// <param name="footers">Receives the bottom margin texts.</param>
    /// <param name="seen">Room to note the texts already counted for this page.</param>
    private static void CountMargins(PdfMarkedContentPage content, Dictionary<string, int> headers, Dictionary<string, int> footers, HashSet<string> seen)
    {
        var items = MarginItems(content);
        if (items.Count == 0)
        {
            return;
        }

        var array = items.ToArray();
        var lines = ArrayPool<LayoutLine>.Shared.Rent(array.Length);
        try
        {
            var lineCount = LayoutLines.Build(array, array.Length, lines);
            seen.Clear();
            for (var i = 0; i < lineCount; i++)
            {
                var band = LayoutPagination.Band(lines[i], content.Height);
                var buffer = ArrayPool<char>.Shared.Rent(LayoutPagination.KeyBufferLength(array, lines[i]));
                try
                {
                    var key = new string(LayoutPagination.Key(content.Text, array, lines[i], buffer));
                    if (key.Length > 0 && seen.Add((band < 0 ? "T" : "B") + key))
                    {
                        var counts = band < 0 ? headers : footers;
                        counts[key] = counts.GetValueOrDefault(key) + 1;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }
        finally
        {
            ArrayPool<LayoutLine>.Shared.Return(lines);
        }
    }

    /// <summary>Lists the glyphs in the top and bottom margins of a page, other than those already marked as artifacts.</summary>
    /// <param name="content">The page.</param>
    /// <returns>The glyphs as layout items.</returns>
    private static List<LayoutItem> MarginItems(PdfMarkedContentPage content)
    {
        var items = new List<LayoutItem>();
        var glyphs = content.Glyphs;
        var probe = new LayoutLine(0, 0, default);
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i];
            if (glyph.IsArtifact || glyph.Bounds.IsEmpty)
            {
                continue;
            }

            if (LayoutPagination.Band(probe with { Bounds = glyph.Bounds }, content.Height) != 0)
            {
                items.Add(new(glyph.Bounds, i, glyph.TextStart, glyph.TextLength));
            }
        }

        return items;
    }
}
