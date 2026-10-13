// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>A contextual rule's instruction to run another lookup at a place in the matched glyphs.</summary>
/// <param name="SequenceIndex">The place among the matched input glyphs, from zero.</param>
/// <param name="LookupIndex">The lookup to run.</param>
public readonly record struct LookupRecord(int SequenceIndex, int LookupIndex);
