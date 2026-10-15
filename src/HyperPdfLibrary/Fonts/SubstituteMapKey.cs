// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// What decides the code map of a non-embedded simple font on a given stand-in face when its encoding has no
/// /Differences and it has no /ToUnicode map.
/// </summary>
/// <param name="Base">The base encoding.</param>
/// <param name="SymbolEncoding">The built-in encoding of a Symbol or ZapfDingbats font, else <see cref="FontEncoding.None"/>.</param>
/// <param name="Symbolic">Whether the descriptor flags say symbolic.</param>
internal readonly record struct SubstituteMapKey(FontEncoding Base, FontEncoding SymbolEncoding, bool Symbolic);
