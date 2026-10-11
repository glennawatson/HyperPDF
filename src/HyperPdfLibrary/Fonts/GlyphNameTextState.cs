// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Stores one-character glyph-name texts.</summary>
[DebuggerDisplay("Glyph name texts")]
internal sealed class GlyphNameTextState
{
    /// <summary>The Latin block's upper bound.</summary>
    internal const int LatinEnd = 0x0300;

    /// <summary>The punctuation block's lower bound.</summary>
    internal const int PunctuationStart = 0x2000;

    /// <summary>The punctuation block's upper bound.</summary>
    internal const int PunctuationEnd = 0x2130;

    /// <summary>Gets cached one-character Latin texts.</summary>
    internal string?[] LatinTexts { get; } = new string?[LatinEnd];

    /// <summary>Gets cached one-character punctuation texts.</summary>
    internal string?[] PunctuationTexts { get; } = new string?[PunctuationEnd - PunctuationStart];
}
