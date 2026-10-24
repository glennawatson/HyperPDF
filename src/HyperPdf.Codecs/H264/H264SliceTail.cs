// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The end of a slice header: the CABAC initialisation, the quantiser change and the deblocking control.</summary>
/// <param name="CabacInitIdc">The number that picks the table CABAC contexts start from.</param>
/// <param name="QpDelta">The change of the quantiser from the picture's start.</param>
/// <param name="DisableDeblockingFilterIdc">What the deblocking filter does at this slice's edges: 0 filters, 1 not at all, 2 not across slice edges.</param>
/// <param name="AlphaC0OffsetDiv2">The offset of the filter strength, divided by 2.</param>
/// <param name="BetaOffsetDiv2">The offset of the filter threshold, divided by 2.</param>
internal readonly record struct H264SliceTail(int CabacInitIdc, int QpDelta, int DisableDeblockingFilterIdc, int AlphaC0OffsetDiv2, int BetaOffsetDiv2);
