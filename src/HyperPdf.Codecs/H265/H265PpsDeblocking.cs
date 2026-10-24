// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The deblocking filter settings of a picture parameter set.</summary>
/// <param name="OverrideEnabled">Whether slice headers may replace these settings.</param>
/// <param name="Disabled">Whether the filter is off.</param>
/// <param name="BetaOffsetDiv2">Half the offset of the filter strength threshold.</param>
/// <param name="TcOffsetDiv2">Half the offset of the clipping threshold.</param>
internal readonly record struct H265PpsDeblocking(bool OverrideEnabled, bool Disabled, int BetaOffsetDiv2, int TcOffsetDiv2);
