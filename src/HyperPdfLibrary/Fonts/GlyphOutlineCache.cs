// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Caches one outline per glyph slot (a code or a glyph id below 65536). Slots live in pages of 256 made on first use,
/// so a font with few glyphs in use stays small. Reads take no lock; a slot is published once with
/// <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>.
/// </summary>
[DebuggerDisplay("GlyphOutlineCache")]
internal sealed class GlyphOutlineCache
{
    /// <summary>The number of slots the cache covers.</summary>
    internal const int SlotCount = PageSize * PageCount;

    /// <summary>The bits of a slot that select its place in a page.</summary>
    private const int PageBits = 8;

    /// <summary>The slots in a page.</summary>
    private const int PageSize = 1 << PageBits;

    /// <summary>The number of pages.</summary>
    private const int PageCount = 256;

    /// <summary>The mask of a slot's place in its page.</summary>
    private const int PageMask = PageSize - 1;

    /// <summary>Marks a slot whose glyph has no outline, so it is not built again.</summary>
    private static readonly PdfPath Blank = PdfPath.Empty;

    /// <summary>The page of the first 256 slots, made on first use; it holds every code of a simple font.</summary>
    private PdfPath?[]? _firstPage;

    /// <summary>The pages beyond the first, made on first use along with the table that holds them.</summary>
    private PdfPath?[]?[]? _pages;

    /// <summary>Gets a slot's outline, building it on first use.</summary>
    /// <typeparam name="TState">The type of the state given to the builder.</typeparam>
    /// <param name="slot">The slot.</param>
    /// <param name="state">The state given to the builder, usually the font.</param>
    /// <param name="build">Builds the outline; returns <see langword="null"/> or an empty path for a blank glyph.</param>
    /// <returns>The outline, or <see langword="null"/> for a blank glyph or a slot out of range.</returns>
    internal PdfPath? GetOrBuild<TState>(int slot, TState state, Func<TState, int, PdfPath?> build)
    {
        if ((uint)slot >= SlotCount)
        {
            return null;
        }

        var page = slot < PageSize ? Volatile.Read(ref _firstPage) ?? CreateFirstPage() : GetPage(slot >> PageBits);
        var path = Volatile.Read(ref page[slot & PageMask]);
        path ??= Publish(page, slot & PageMask, build(state, slot));

        return ReferenceEquals(path, Blank) ? null : path;
    }

    /// <summary>Stores a built outline unless another thread stored one first.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The slot's place in the page.</param>
    /// <param name="built">The outline that was built.</param>
    /// <returns>The outline now in the slot.</returns>
    private static PdfPath Publish(PdfPath?[] page, int index, PdfPath? built)
    {
        var made = built is null || built.IsEmpty ? Blank : built;
        var existing = Interlocked.CompareExchange(ref page[index], made, null);
        return existing ?? made;
    }

    /// <summary>Makes a page, keeping another thread's page if it won.</summary>
    /// <param name="pages">The table of pages.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The page.</returns>
    private static PdfPath?[] CreatePage(PdfPath?[]?[] pages, int index)
    {
        var page = new PdfPath?[PageSize];
        return Interlocked.CompareExchange(ref pages[index], page, null) ?? page;
    }

    /// <summary>Gets a page beyond the first, making the table of pages and the page when they are not there.</summary>
    /// <param name="index">The page index, from 1.</param>
    /// <returns>The page.</returns>
    private PdfPath?[] GetPage(int index)
    {
        var pages = Volatile.Read(ref _pages) ?? CreatePages();
        return Volatile.Read(ref pages[index]) ?? CreatePage(pages, index);
    }

    /// <summary>Makes the first page, which a simple font's single-byte codes all fit in, keeping another thread's page if it won.</summary>
    /// <returns>The page.</returns>
    private PdfPath?[] CreateFirstPage()
    {
        var page = new PdfPath?[PageSize];
        return Interlocked.CompareExchange(ref _firstPage, page, null) ?? page;
    }

    /// <summary>Makes the table of pages, keeping another thread's table if it won.</summary>
    /// <returns>The table.</returns>
    private PdfPath?[]?[] CreatePages()
    {
        var pages = new PdfPath?[]?[PageCount];
        return Interlocked.CompareExchange(ref _pages, pages, null) ?? pages;
    }
}
