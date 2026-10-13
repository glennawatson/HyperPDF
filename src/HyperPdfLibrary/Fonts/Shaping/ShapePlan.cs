// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The lookups to run for one script, language, direction and set of features, stage by stage.</summary>
/// <param name="Substitution">The GSUB lookups of each stage.</param>
/// <param name="Positioning">The GPOS lookups of each stage.</param>
/// <param name="UsesLegacyKerning">Whether the font has no GPOS kerning, so the kern table is used.</param>
[DebuggerDisplay("ShapePlan: {Substitution.Length} substitution stages")]
internal sealed record ShapePlan(LookupEntry[][] Substitution, LookupEntry[][] Positioning, bool UsesLegacyKerning);
