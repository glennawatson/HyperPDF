// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The colour settings of a sequence parameter set.</summary>
/// <param name="Format">The chroma format.</param>
/// <param name="SeparatePlanes">Whether the colour planes are coded separately.</param>
/// <param name="DepthLuma">The bits of a luma sample.</param>
/// <param name="DepthChroma">The bits of a chroma sample.</param>
/// <param name="Bypass">Whether blocks may bypass the transform when the quantiser is 0.</param>
/// <param name="Scaling">The scaling lists the set carries, or <see langword="null"/>.</param>
internal sealed record H264Colour(int Format, bool SeparatePlanes, int DepthLuma, int DepthChroma, bool Bypass, H264ScalingLists? Scaling);
