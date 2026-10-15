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

    /// <summary>The number of glyphs the tables hold before they first grow, which covers a page of ordinary text in one allocation each.</summary>
    private const int InitialGlyphs = 96;

    /// <summary>The number of characters whose one-character string is shared.</summary>
    private const int SharedTexts = 256;

    /// <summary>The immutable one-character strings for the first <see cref="SharedTexts"/> characters.</summary>
    private static readonly string[] OneCharacter = CreateCharacterTexts();

    /// <summary>Guards every field.</summary>
    private readonly Lock _gate = new();

    /// <summary>The code of each glyph shown.</summary>
    private readonly Dictionary<int, int> _codes = [with(InitialGlyphs)];

    /// <summary>The glyphs shown, in the order they were first used.</summary>
    private readonly List<int> _order = [with(InitialGlyphs)];

    /// <summary>The text of each code.</summary>
    private readonly Dictionary<int, string> _texts = [with(InitialGlyphs)];

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
    internal bool RecordText(int code, ReadOnlySpan<char> text)
    {
        if (code == 0 || text.IsEmpty)
        {
            return true;
        }

        lock (_gate)
        {
            if (_texts.TryGetValue(code, out var existing))
            {
                return text.SequenceEqual(existing);
            }

            // The snapshot shares this dictionary after freezing, so only unfrozen fonts add text.
            if (!_frozen)
            {
                _texts[code] = TextOf(text);
            }

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

            return new(glyphs, codes, _texts);
        }
    }

    /// <summary>Gets a string for some text, shared when it is one early character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string.</returns>
    private static string TextOf(ReadOnlySpan<char> text) =>
        text.Length != 1 || text[0] >= SharedTexts ? text.ToString() : OneCharacter[text[0]];

    /// <summary>Builds the fixed character table once; no entry is changed after initialization.</summary>
    /// <returns>The text for each character.</returns>
    private static string[] CreateCharacterTexts()
    {
        var values = new string[SharedTexts];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new((char)i, 1);
        }

        return values;
    }

    /// <summary>Gets the fixed code of a glyph.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The code, or zero when the glyph is outside the table.</returns>
    private int Fixed(int glyph) => _fixedCodes is not null && glyph < _fixedCodes.Length ? _fixedCodes[glyph] : 0;
}
