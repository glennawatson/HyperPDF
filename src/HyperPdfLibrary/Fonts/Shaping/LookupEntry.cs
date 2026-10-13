// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>A lookup to run in a stage of a shaping plan.</summary>
/// <param name="Lookup">The index in the lookup list.</param>
/// <param name="Mask">The glyph mask bits of the features that use the lookup; the lookup applies to glyphs that have one of them.</param>
/// <param name="Value">The value of the feature: for an alternate substitution, the one-based alternate to choose.</param>
[DebuggerDisplay("LookupEntry: lookup {Lookup}")]
internal readonly record struct LookupEntry(int Lookup, uint Mask, int Value);
