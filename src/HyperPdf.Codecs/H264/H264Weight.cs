// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The weights and offsets a slice gives one reference picture.</summary>
/// <param name="LumaWeight">The luma weight.</param>
/// <param name="LumaOffset">The luma offset.</param>
/// <param name="CbWeight">The Cb weight.</param>
/// <param name="CbOffset">The Cb offset.</param>
/// <param name="CrWeight">The Cr weight.</param>
/// <param name="CrOffset">The Cr offset.</param>
/// <param name="HasLuma">Whether the slice sent luma weights; otherwise the default weight applies.</param>
/// <param name="HasChroma">Whether the slice sent chroma weights; otherwise the default weight applies.</param>
internal readonly record struct H264Weight(int LumaWeight, int LumaOffset, int CbWeight, int CbOffset, int CrWeight, int CrOffset, bool HasLuma, bool HasChroma);
