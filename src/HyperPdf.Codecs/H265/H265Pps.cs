// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>A picture parameter set: the settings that can change from picture to picture.</summary>
/// <param name="Id">The set's number.</param>
/// <param name="SpsId">The number of the sequence parameter set it belongs to.</param>
/// <param name="Flags">The on or off settings.</param>
/// <param name="ExtraSliceHeaderBits">The reserved bits at the start of a slice header.</param>
/// <param name="NumRefIdxL0Default">The default number of reference pictures in list 0.</param>
/// <param name="NumRefIdxL1Default">The default number of reference pictures in list 1.</param>
/// <param name="Quant">The quantiser settings.</param>
/// <param name="Tiles">The tile layout.</param>
/// <param name="Deblocking">The deblocking filter settings.</param>
/// <param name="Scaling">The quantisation matrices, or <see langword="null"/> to use those of the sequence parameter set.</param>
/// <param name="Log2ParMrgLevel">The base 2 logarithm of the size of the area that shares merge candidates.</param>
/// <param name="Range">The range extension settings.</param>
[DebuggerDisplay("H265Pps: {Id} for sequence set {SpsId}")]
internal sealed record H265Pps(
    int Id,
    int SpsId,
    H265PpsFlags Flags,
    int ExtraSliceHeaderBits,
    int NumRefIdxL0Default,
    int NumRefIdxL1Default,
    H265PpsQuant Quant,
    H265TileSpec Tiles,
    H265PpsDeblocking Deblocking,
    H265ScalingLists? Scaling,
    int Log2ParMrgLevel,
    H265PpsRange Range);
