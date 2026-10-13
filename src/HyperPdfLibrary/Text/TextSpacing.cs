// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Decides when the gap between two glyphs on a line is a word space. A producer that positions words separately leaves
/// no space glyph, so the gap from where one glyph's advance ends to where the next glyph starts is compared with a share
/// of the font's own space width: half of it, with the space width limited to 0.2 to 0.5 em. Real word gaps are at least
/// about a third of an em, while kerning and tracking adjustments rarely exceed a tenth.
/// </summary>
internal static class TextSpacing
{
    /// <summary>The gap, in em, at or below which no font lookup is needed because it cannot be a space.</summary>
    internal const float NeverSpaceEm = MinSpaceEm * GapShare;

    /// <summary>The space width, in em, assumed for a font with no usable space glyph.</summary>
    private const float DefaultSpaceEm = 0.25F;

    /// <summary>The narrowest space width, in em, that sets the threshold.</summary>
    private const float MinSpaceEm = 0.2F;

    /// <summary>The widest space width, in em, that sets the threshold.</summary>
    private const float MaxSpaceEm = 0.5F;

    /// <summary>The share of the space width a gap must pass to count as a space.</summary>
    private const float GapShare = 0.5F;

    /// <summary>The highest single-byte code searched for a font's space.</summary>
    private const int MaxSpaceCode = 255;

    /// <summary>The size of the buffer a code's text is written to.</summary>
    private const int UnicodeBufferLength = 8;

    /// <summary>Gets the gap, in em, that a gap must exceed to be a space.</summary>
    /// <param name="state">The build state, which caches each font's answer.</param>
    /// <param name="font">The font of the glyph before the gap.</param>
    /// <returns>The threshold in em.</returns>
    internal static float ThresholdEm(TextPageBuildState state, PdfFont font)
    {
        ref var threshold = ref CollectionsMarshal.GetValueRefOrAddDefault(state.SpaceWidths, font, out var known);
        if (!known)
        {
            threshold = GapShare * Math.Clamp(FindSpaceWidth(font), MinSpaceEm, MaxSpaceEm);
        }

        return threshold;
    }

    /// <summary>Finds the width, in em, of the glyph a font shows as a space.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The width, or a default when the font has no single-byte space.</returns>
    private static float FindSpaceWidth(PdfFont font)
    {
        var code = IsSpace(font, ' ') ? ' ' : -1;
        for (var candidate = 0; code < 0 && candidate <= MaxSpaceCode; candidate++)
        {
            code = IsSpace(font, candidate) ? candidate : -1;
        }

        return code >= 0 ? font.GetWidth(code) : DefaultSpaceEm;
    }

    /// <summary>Determines whether a code's text is a single space.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsSpace(PdfFont font, int code)
    {
        Span<char> text = stackalloc char[UnicodeBufferLength];
        return font.GetUnicode(code, text) == 1 && text[0] == ' ';
    }
}
