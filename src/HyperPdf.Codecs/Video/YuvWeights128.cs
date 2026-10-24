// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>The weights of a <see cref="ColorMatrix"/> spread across the lanes of 128 bit vectors.</summary>
/// <param name="Offset">The luma value that is black.</param>
/// <param name="Scale">The weight of luma.</param>
/// <param name="RedFromCr">The weight of Cr in red.</param>
/// <param name="GreenFromCb">The weight of Cb in green.</param>
/// <param name="GreenFromCr">The weight of Cr in green.</param>
/// <param name="BlueFromCb">The weight of Cb in blue.</param>
internal readonly record struct YuvWeights128(
    Vector128<int> Offset,
    Vector128<int> Scale,
    Vector128<int> RedFromCr,
    Vector128<int> GreenFromCb,
    Vector128<int> GreenFromCr,
    Vector128<int> BlueFromCb)
{
    /// <summary>Spreads the weights of a matrix across vector lanes.</summary>
    /// <param name="matrix">The weights.</param>
    /// <returns>The lanes.</returns>
    internal static YuvWeights128 From(in ColorMatrix matrix) => new(
        Vector128.Create(matrix.LumaOffset),
        Vector128.Create(matrix.LumaScale),
        Vector128.Create(matrix.RedFromCr),
        Vector128.Create(matrix.GreenFromCb),
        Vector128.Create(matrix.GreenFromCr),
        Vector128.Create(matrix.BlueFromCb));

    /// <summary>Converts four pixels.</summary>
    /// <param name="luma">The luma samples.</param>
    /// <param name="cb">The Cb samples, one for each pixel.</param>
    /// <param name="cr">The Cr samples, one for each pixel.</param>
    /// <returns>The pixels, as blue in the lowest byte of each lane and alpha in the highest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector128<int> Pixels(Vector128<int> luma, Vector128<int> cb, Vector128<int> cr)
    {
        var blueDifference = cb - Vector128.Create(YuvConstants.ColorCentre);
        var redDifference = cr - Vector128.Create(YuvConstants.ColorCentre);
        var common = ((luma - Offset) * Scale) + Vector128.Create(YuvConstants.Rounding);
        var red = Channel(common + (redDifference * RedFromCr));
        var green = Channel(common + (blueDifference * GreenFromCb) + (redDifference * GreenFromCr));
        var blue = Channel(common + (blueDifference * BlueFromCb));
        return blue | Vector128.ShiftLeft(green, YuvConstants.GreenShift) | Vector128.ShiftLeft(red, YuvConstants.RedShift) | Vector128.Create(YuvConstants.OpaqueAlpha);
    }

    /// <summary>Drops the fractional bits of a channel and keeps it within a byte.</summary>
    /// <param name="value">The channel with its fractional bits.</param>
    /// <returns>The channel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Channel(Vector128<int> value) =>
        Vector128.Min(Vector128.Max(Vector128.ShiftRightArithmetic(value, YuvConstants.FractionBits), Vector128<int>.Zero), Vector128.Create(YuvConstants.ChannelMax));
}
