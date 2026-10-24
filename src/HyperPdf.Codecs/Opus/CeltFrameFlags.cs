// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The flags at the start of a CELT frame.</summary>
/// <param name="Silence">Whether the frame is silent.</param>
/// <param name="PostFilter">The comb filter that follows the frame.</param>
/// <param name="Transient">Whether the frame is split into short blocks.</param>
/// <param name="Intra">Whether the band energies are coded without prediction from the last frame.</param>
internal readonly record struct CeltFrameFlags(bool Silence, CeltPostFilterSettings PostFilter, bool Transient, bool Intra);
