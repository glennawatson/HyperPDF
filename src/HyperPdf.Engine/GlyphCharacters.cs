// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Engine;

/// <summary>The page characters one glyph became: usually one, more for a ligature, none when it has no match.</summary>
/// <param name="Start">The first character index, or -1.</param>
/// <param name="Count">The number of characters.</param>
[DebuggerDisplay("GlyphCharacters: {Start} + {Count}")]
internal readonly record struct GlyphCharacters(int Start, int Count);
