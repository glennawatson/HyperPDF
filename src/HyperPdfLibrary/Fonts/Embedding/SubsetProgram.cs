// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>A font program cut down to some glyphs, with the glyphs numbered afresh from zero.</summary>
/// <param name="Data">The font program.</param>
/// <param name="Original">The original glyph of each new glyph, in new glyph order; the first is always the missing glyph.</param>
/// <param name="IsCidKeyed">Whether the program is CID-keyed, so its CIDs are its glyph numbers; a name-keyed program is kept only when an accented glyph needs its names.</param>
[DebuggerDisplay("SubsetProgram: {Original.Length} glyphs, {Data.Length} bytes")]
internal sealed record SubsetProgram(byte[] Data, int[] Original, bool IsCidKeyed);
