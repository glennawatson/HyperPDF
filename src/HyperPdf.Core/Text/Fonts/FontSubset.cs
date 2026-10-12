// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>A font cut down to the glyphs some text uses, with the glyphs numbered afresh from 0.</summary>
[DebuggerDisplay("FontSubset: {GlyphCount} glyphs, {Data.Length} bytes")]
public sealed class FontSubset
{
    /// <summary>The new number of each original glyph, or 0 when it was left out.</summary>
    private readonly ushort[] _map;

    /// <summary>Initializes a new instance of the <see cref="FontSubset"/> class.</summary>
    /// <param name="data">The subset font file.</param>
    /// <param name="map">The new number of each original glyph.</param>
    /// <param name="original">The original glyph of each new one.</param>
    internal FontSubset(byte[] data, ushort[] map, ushort[] original)
    {
        Data = data;
        _map = map;
        Original = original;
    }

    /// <summary>Gets the subset font file, a standalone TrueType font.</summary>
    public byte[] Data { get; }

    /// <summary>Gets the original glyph of each new glyph, in new glyph order.</summary>
    public IReadOnlyList<ushort> Original { get; }

    /// <summary>Gets the number of glyphs kept.</summary>
    public int GlyphCount => Original.Count;

    /// <summary>Gets the new number of an original glyph.</summary>
    /// <param name="glyph">The original glyph.</param>
    /// <returns>The new glyph, or 0 (the missing glyph) when it was left out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Map(ushort glyph) => glyph < _map.Length ? _map[glyph] : (ushort)0;

    /// <summary>Determines whether an original glyph was kept.</summary>
    /// <param name="glyph">The original glyph.</param>
    /// <returns><see langword="true"/> when kept; the missing glyph is always kept.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ushort glyph) => glyph == 0 || Map(glyph) != 0;
}
