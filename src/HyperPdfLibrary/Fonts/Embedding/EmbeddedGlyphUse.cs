// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Records the glyphs an embedded font has shown and the text each stands for. Glyphs get codes on first use: the next
/// number in order, so the codes written into content streams are the dense CIDs the subset font is built with, unless
/// the font program fixes each glyph's CID.
/// </summary>
[DebuggerDisplay("EmbeddedGlyphUse: {_order.Count} glyphs")]
internal sealed class EmbeddedGlyphUse
{
    /// <summary>The largest code a two-byte string can hold.</summary>
    internal const int MaxCode = 0xFFFF;

    /// <summary>Guards every field.</summary>
    private readonly Lock _gate = new();

    /// <summary>The code of each glyph shown.</summary>
    private readonly Dictionary<int, int> _codes = [];

    /// <summary>The glyphs shown, in the order they were first used.</summary>
    private readonly List<int> _order = [];

    /// <summary>The text of each code.</summary>
    private readonly Dictionary<int, string> _texts = [];

    /// <summary>The CID of each glyph when the font program fixes them, or <see langword="null"/> to number glyphs densely.</summary>
    private readonly int[]? _fixedCodes;

    /// <summary>The number of glyphs in the font.</summary>
    private readonly int _glyphCount;

    /// <summary>Whether the font is finished and no new glyph can be used.</summary>
    private bool _frozen;

    /// <summary>Initializes a new instance of the <see cref="EmbeddedGlyphUse"/> class.</summary>
    /// <param name="glyphCount">The number of glyphs in the font.</param>
    /// <param name="fixedCodes">The CID of each glyph when the font program fixes them, or <see langword="null"/> to number glyphs densely from one.</param>
    internal EmbeddedGlyphUse(int glyphCount, int[]? fixedCodes)
    {
        _glyphCount = glyphCount;
        _fixedCodes = fixedCodes;
    }

    /// <summary>Gets the number of glyphs shown, not counting the missing glyph.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _order.Count;
            }
        }
    }

    /// <summary>Gets the code of a glyph, giving it the next one on first use.</summary>
    /// <param name="glyph">The glyph in the font file.</param>
    /// <returns>The code; zero (the missing glyph) when the glyph is not in the font or the codes have run out.</returns>
    /// <exception cref="InvalidOperationException">The font is finished and the glyph was never used.</exception>
    internal int GetCode(int glyph)
    {
        if (glyph <= 0 || glyph >= _glyphCount)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_codes.TryGetValue(glyph, out var known))
            {
                return known;
            }

            if (_frozen)
            {
                throw new InvalidOperationException("The embedded font is finished, so it cannot show a glyph it has not used before.");
            }

            var code = _fixedCodes is null ? _order.Count + 1 : Fixed(glyph);
            if (code is <= 0 or > MaxCode)
            {
                return 0;
            }

            _codes[glyph] = code;
            _order.Add(glyph);
            return code;
        }
    }

    /// <summary>Records the text a code stands for, if it has none yet.</summary>
    /// <param name="code">The code.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the code now stands for this text; <see langword="false"/> when it already stands for different text.</returns>
    internal bool RecordText(int code, string text)
    {
        if (code == 0 || text.Length == 0)
        {
            return true;
        }

        lock (_gate)
        {
            if (_texts.TryGetValue(code, out var existing))
            {
                return string.Equals(existing, text, StringComparison.Ordinal);
            }

            _texts[code] = text;
            return true;
        }
    }

    /// <summary>Stops new glyphs being used and takes a copy of what was recorded.</summary>
    /// <returns>The glyphs in code order and the text of each code.</returns>
    internal GlyphUseSnapshot Freeze()
    {
        lock (_gate)
        {
            _frozen = true;
            var glyphs = _order.ToArray();
            var codes = new int[glyphs.Length];
            for (var i = 0; i < glyphs.Length; i++)
            {
                codes[i] = _codes[glyphs[i]];
            }

            if (_fixedCodes is not null)
            {
                Array.Sort(codes, glyphs);
            }

            return new(glyphs, codes, new Dictionary<int, string>(_texts));
        }
    }

    /// <summary>Gets the fixed code of a glyph.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The code, or zero when the glyph is outside the table.</returns>
    private int Fixed(int glyph) => _fixedCodes is not null && glyph < _fixedCodes.Length ? _fixedCodes[glyph] : 0;
}
