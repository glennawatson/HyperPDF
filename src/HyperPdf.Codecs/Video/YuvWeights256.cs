// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>The weights of a <see cref="ColorMatrix"/> spread across the lanes of 256 bit vectors.</summary>
/// <param name="Offset">The luma value that is black.</param>
/// <param name="Scale">The weight of luma.</param>
/// <param name="RedFromCr">The weight of Cr in red.</param>
/// <param name="GreenFromCb">The weight of Cb in green.</param>
/// <param name="GreenFromCr">The weight of Cr in green.</param>
/// <param name="BlueFromCb">The weight of Cb in blue.</param>
internal readonly record struct YuvWeights256(
    Vector256<int> Offset,
    Vector256<int> Scale,
    Vector256<int> RedFromCr,
    Vector256<int> GreenFromCb,
    Vector256<int> GreenFromCr,
    Vector256<int> BlueFromCb)
{
    /// <summary>Spreads the weights of a matrix across vector lanes.</summary>
    /// <param name="matrix">The weights.</param>
    /// <returns>The lanes.</returns>
    internal static YuvWeights256 From(in ColorMatrix matrix) => new(
        Vector256.Create(matrix.LumaOffset),
        Vector256.Create(matrix.LumaScale),
        Vector256.Create(matrix.RedFromCr),
        Vector256.Create(matrix.GreenFromCb),
        Vector256.Create(matrix.GreenFromCr),
        Vector256.Create(matrix.BlueFromCb));

    /// <summary>Converts eight pixels.</summary>
    /// <param name="luma">The luma samples.</param>
    /// <param name="cb">The Cb samples, one for each pixel.</param>
    /// <param name="cr">The Cr samples, one for each pixel.</param>
    /// <returns>The pixels, as blue in the lowest byte of each lane and alpha in the highest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector256<int> Pixels(Vector256<int> luma, Vector256<int> cb, Vector256<int> cr)
    {
        var blueDifference = cb - Vector256.Create(YuvConstants.ColorCentre);
        var redDifference = cr - Vector256.Create(YuvConstants.ColorCentre);
        var common = ((luma - Offset) * Scale) + Vector256.Create(YuvConstants.Rounding);
        var red = Channel(common + (redDifference * RedFromCr));
        var green = Channel(common + (blueDifference * GreenFromCb) + (redDifference * GreenFromCr));
        var blue = Channel(common + (blueDifference * BlueFromCb));
        return blue | Vector256.ShiftLeft(green, YuvConstants.GreenShift) | Vector256.ShiftLeft(red, YuvConstants.RedShift) | Vector256.Create(YuvConstants.OpaqueAlpha);
    }

    /// <summary>Drops the fractional bits of a channel and keeps it within a byte.</summary>
    /// <param name="value">The channel with its fractional bits.</param>
    /// <returns>The channel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Channel(Vector256<int> value) =>
        Vector256.Min(Vector256.Max(Vector256.ShiftRightArithmetic(value, YuvConstants.FractionBits), Vector256<int>.Zero), Vector256.Create(YuvConstants.ChannelMax));
}
