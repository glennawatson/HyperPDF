// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Explicit checked-pixel counts and channel errors; no whole-image percentage hides missing content.</summary>
/// <param name="DimensionsMatch">Whether the geometry matches.</param>
/// <param name="CheckedPixels">The number of distinct pixels checked.</param>
/// <param name="ErrorPixels">The number of pixels exceeding the stated tolerance.</param>
/// <param name="BlueAbsoluteError">The sum of blue-channel absolute errors.</param>
/// <param name="GreenAbsoluteError">The sum of green-channel absolute errors.</param>
/// <param name="RedAbsoluteError">The sum of red-channel absolute errors.</param>
/// <param name="AlphaAbsoluteError">The sum of alpha-channel absolute errors.</param>
/// <param name="MaximumChannelError">The largest observed channel error.</param>
/// <param name="AllowedDifferencePixels">Pixels differing only within the declared standards allowance.</param>
internal readonly record struct RasterScore(
    bool DimensionsMatch,
    long CheckedPixels,
    long ErrorPixels,
    long BlueAbsoluteError,
    long GreenAbsoluteError,
    long RedAbsoluteError,
    long AlphaAbsoluteError,
    byte MaximumChannelError,
    long AllowedDifferencePixels)
{
    /// <summary>Gets whether all checked pixels satisfy this score's expectations.</summary>
    internal bool IsAcceptable => DimensionsMatch && CheckedPixels > 0 && ErrorPixels == 0;

    /// <summary>Gets the absolute error across all four channels.</summary>
    internal long TotalChannelError => BlueAbsoluteError + GreenAbsoluteError + RedAbsoluteError + AlphaAbsoluteError;
}
