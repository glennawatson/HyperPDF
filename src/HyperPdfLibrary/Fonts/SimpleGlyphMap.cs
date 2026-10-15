// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>The glyph and Unicode text of each code of a simple font, while it is being loaded.</summary>
[DebuggerDisplay("SimpleGlyphMap")]
internal sealed class SimpleGlyphMap
{
    /// <summary>The glyph value meaning the code has no glyph.</summary>
    internal const int NoGlyph = -1;

    /// <summary>Initializes a new instance of the <see cref="SimpleGlyphMap"/> class with no glyphs.</summary>
    internal SimpleGlyphMap() => Glyphs.AsSpan().Fill(NoGlyph);

    /// <summary>Gets the glyph id of each code, or <see cref="NoGlyph"/>.</summary>
    internal int[] Glyphs { get; } = new int[FontEncodings.CodeCount];

    /// <summary>Gets the Unicode text of each code from its glyph name, or <see langword="null"/>.</summary>
    internal string?[] Texts { get; } = new string?[FontEncodings.CodeCount];

    /// <summary>Makes an independent copy of the map.</summary>
    /// <returns>The copy.</returns>
    internal SimpleGlyphMap Copy()
    {
        var copy = new SimpleGlyphMap();
        Glyphs.CopyTo(copy.Glyphs, 0);
        Texts.CopyTo(copy.Texts, 0);
        return copy;
    }

    /// <summary>Determines whether any code has a glyph other than zero (glyph 0 is .notdef).</summary>
    /// <returns><see langword="true"/> when some code maps to a glyph.</returns>
    internal bool HasAnyGlyph()
    {
        foreach (var glyph in Glyphs)
        {
            if (glyph != 0)
            {
                return true;
            }
        }

        return false;
    }
}
