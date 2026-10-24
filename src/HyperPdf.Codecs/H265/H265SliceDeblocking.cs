// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The loop filter settings of a slice.</summary>
/// <param name="Disabled">Whether the deblocking filter is off.</param>
/// <param name="BetaOffsetDiv2">Half the offset of the filter strength threshold.</param>
/// <param name="TcOffsetDiv2">Half the offset of the clipping threshold.</param>
/// <param name="LoopFilterAcrossSlices">Whether the loop filters may cross the left and upper edges of the slice.</param>
internal readonly record struct H265SliceDeblocking(bool Disabled, int BetaOffsetDiv2, int TcOffsetDiv2, bool LoopFilterAcrossSlices);
