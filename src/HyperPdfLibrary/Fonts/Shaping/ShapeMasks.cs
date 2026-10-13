// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The bits of a glyph's feature mask. A lookup applies to a glyph when the glyph has one of the bits of the features that use the lookup.</summary>
internal static class ShapeMasks
{
    /// <summary>Set on every glyph; the features that apply everywhere use it.</summary>
    internal const uint Global = 1;

    /// <summary>Isolated Arabic form (isol).</summary>
    internal const uint Isolated = 1 << 1;

    /// <summary>Terminal Arabic form (fina).</summary>
    internal const uint Final = 1 << 2;

    /// <summary>Medial Arabic form (medi).</summary>
    internal const uint Medial = 1 << 3;

    /// <summary>Initial Arabic form (init).</summary>
    internal const uint Initial = 1 << 4;

    /// <summary>The ra of an Indic syllable that may take the reph form (rphf).</summary>
    internal const uint Reph = 1 << 5;

    /// <summary>A consonant of an Indic syllable that comes before its base, which may take a half form (half).</summary>
    internal const uint PreBase = 1 << 6;
}
