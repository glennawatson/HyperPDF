// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Keeps the coverage of the glyphs a canvas has drawn, so a glyph that comes again at the same size and the same place inside a
/// pixel is copied and not flattened and filled again. A page of text draws the same few hundred glyphs thousands of times.
/// The coverage of every glyph lives in one block of bytes of a fixed size. When the block is full everything is forgotten and
/// the cache starts again, so it never grows, however many documents are drawn, and it holds on to the outlines of a closed
/// document only until then. Each canvas has its own, and a canvas draws on one thread, so nothing here is shared.
/// </summary>
[DebuggerDisplay("GlyphCoverageCache: {_glyphs.Count} glyphs, {_used} bytes")]
internal sealed class GlyphCoverageCache
{
    /// <summary>The bytes of coverage kept, which is a few thousand glyphs of body text.</summary>
    private const int Capacity = 1 << 21;

    /// <summary>Where the coverage of each glyph is.</summary>
    private readonly Dictionary<GlyphCoverageKey, GlyphCoverage> _glyphs = [];

    /// <summary>The coverage of every glyph kept, one after another. Empty until the first glyph is kept.</summary>
    private byte[] _bytes = [];

    /// <summary>The bytes of the block in use.</summary>
    private int _used;

    /// <summary>Gets the number of glyphs kept.</summary>
    internal int Count => _glyphs.Count;

    /// <summary>Finds the coverage kept for a glyph.</summary>
    /// <param name="key">The glyph as it is drawn.</param>
    /// <param name="coverage">Receives where its coverage is.</param>
    /// <returns><see langword="true"/> when it is kept.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGet(in GlyphCoverageKey key, out GlyphCoverage coverage) => _glyphs.TryGetValue(key, out coverage);

    /// <summary>Makes room for the coverage of a glyph and remembers where it is. The caller fills the room.</summary>
    /// <param name="key">The glyph as it is drawn.</param>
    /// <param name="left">The first pixel across it covers, counted from the pixel of the glyph's origin.</param>
    /// <param name="top">The first line it covers, counted from the pixel of the glyph's origin.</param>
    /// <param name="width">The pixels across it covers.</param>
    /// <param name="height">The lines it covers.</param>
    /// <returns>Where its coverage is.</returns>
    internal GlyphCoverage Add(in GlyphCoverageKey key, int left, int top, int width, int height)
    {
        var size = width * height;
        if (_bytes.Length == 0)
        {
            _bytes = new byte[Capacity];
        }

        if (size > Capacity - _used)
        {
            // Starting again is cheaper than choosing what to drop: the glyphs still in use come back as they are drawn.
            _glyphs.Clear();
            _used = 0;
        }

        var coverage = new GlyphCoverage(_used, left, top, width, height);
        _used += size;
        _glyphs[key] = coverage;
        return coverage;
    }

    /// <summary>Gets one line of the coverage of a glyph.</summary>
    /// <param name="coverage">Where its coverage is.</param>
    /// <param name="row">The line, from the top of the coverage.</param>
    /// <returns>One byte a pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> Row(in GlyphCoverage coverage, int row) => _bytes.AsSpan(coverage.Offset + (row * coverage.Width), coverage.Width);

    /// <summary>Forgets every glyph and gives the block of bytes up.</summary>
    internal void Clear()
    {
        _glyphs.Clear();
        _bytes = [];
        _used = 0;
    }
}
