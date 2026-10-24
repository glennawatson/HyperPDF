// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The quantiser settings of a picture parameter set.</summary>
/// <param name="InitQp">The quantiser slices start from, 26 plus the coded offset.</param>
/// <param name="CuQpDeltaEnabled">Whether coding units may change the quantiser.</param>
/// <param name="DiffCuQpDeltaDepth">How many levels below the coding tree block the quantiser may change.</param>
/// <param name="CbQpOffset">The offset of the Cb quantiser from the luma quantiser.</param>
/// <param name="CrQpOffset">The offset of the Cr quantiser from the luma quantiser.</param>
internal readonly record struct H265PpsQuant(int InitQp, bool CuQpDeltaEnabled, int DiffCuQpDeltaDepth, int CbQpOffset, int CrQpOffset);
