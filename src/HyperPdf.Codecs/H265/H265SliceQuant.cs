// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The quantiser settings of a slice.</summary>
/// <param name="SliceQp">The quantiser the slice starts from.</param>
/// <param name="CbQpOffset">The slice's addition to the Cb quantiser offset.</param>
/// <param name="CrQpOffset">The slice's addition to the Cr quantiser offset.</param>
/// <param name="CuChromaQpOffsetEnabled">Whether coding units may pick a chroma quantiser offset.</param>
internal readonly record struct H265SliceQuant(int SliceQp, int CbQpOffset, int CrQpOffset, bool CuChromaQpOffsetEnabled);
