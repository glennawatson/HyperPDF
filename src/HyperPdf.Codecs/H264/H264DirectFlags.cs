// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How direct prediction works in a B slice.</summary>
/// <param name="Spatial">Whether it works from the neighbours (spatial) rather than from the co-located picture (temporal).</param>
/// <param name="Inference">Whether it works on 8x8 blocks, taking the motion of the macroblock's corners (direct_8x8_inference_flag).</param>
internal readonly record struct H264DirectFlags(bool Spatial, bool Inference);
