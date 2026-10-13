// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>What an embedded font showed, as it stood when the font was finished.</summary>
/// <param name="Glyphs">The glyphs shown, in ascending code order, not counting the missing glyph.</param>
/// <param name="Codes">The code of each glyph in <paramref name="Glyphs"/>.</param>
/// <param name="Texts">The text of each code that has any.</param>
[DebuggerDisplay("GlyphUseSnapshot: {Glyphs.Length} glyphs")]
internal sealed record GlyphUseSnapshot(int[] Glyphs, int[] Codes, Dictionary<int, string> Texts);
