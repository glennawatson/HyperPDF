// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How the deblocking filter treats the macroblocks of a slice.</summary>
/// <param name="Mode">disable_deblocking_filter_idc: 0 filters every edge, 1 filters none, 2 filters all but the edges between slices.</param>
/// <param name="AlphaOffset">The offset added to the quantiser when looking up alpha, already doubled.</param>
/// <param name="BetaOffset">The offset added to the quantiser when looking up beta, already doubled.</param>
internal readonly record struct H264SliceFilter(int Mode, int AlphaOffset, int BetaOffset);
