// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Video;

/// <summary>
/// The whole number weights that turn 8 bit Y, Cb and Cr samples into red, green and blue, with 8 fractional bits:
/// <c>R = (LumaScale * (Y - LumaOffset) + RedFromCr * (Cr - 128) + 128) &gt;&gt; 8</c>, and likewise for green and blue.
/// </summary>
/// <param name="LumaOffset">The luma value that is black: 16 for limited range video, 0 for full range.</param>
/// <param name="LumaScale">The weight of luma, 298 for limited range and 256 for full range.</param>
/// <param name="RedFromCr">The weight of Cr in red.</param>
/// <param name="GreenFromCb">The weight of Cb in green, which is negative.</param>
/// <param name="GreenFromCr">The weight of Cr in green, which is negative.</param>
/// <param name="BlueFromCb">The weight of Cb in blue.</param>
[DebuggerDisplay("ColorMatrix: scale {LumaScale}, offset {LumaOffset}")]
public readonly record struct ColorMatrix(int LumaOffset, int LumaScale, int RedFromCr, int GreenFromCb, int GreenFromCr, int BlueFromCb)
{
    /// <summary>The first picture height, in lines, that is taken to be high definition and so uses the BT.709 weights.</summary>
    private const int HighDefinitionHeight = 720;

    /// <summary>The luma value that is black in limited range video.</summary>
    private const int LimitedBlack = 16;

    /// <summary>The weight of luma in limited range video: 255 / 219 with 8 fractional bits.</summary>
    private const int LimitedScale = 298;

    /// <summary>The weight of luma in full range video.</summary>
    private const int FullScale = 256;

    /// <summary>The weight of Cr in red for BT.601 limited range.</summary>
    private const int Bt601LimitedRedFromCr = 409;

    /// <summary>The weight of Cb in green for BT.601 limited range.</summary>
    private const int Bt601LimitedGreenFromCb = -100;

    /// <summary>The weight of Cr in green for BT.601 limited range.</summary>
    private const int Bt601LimitedGreenFromCr = -208;

    /// <summary>The weight of Cb in blue for BT.601 limited range.</summary>
    private const int Bt601LimitedBlueFromCb = 516;

    /// <summary>The weight of Cr in red for BT.709 limited range.</summary>
    private const int Bt709LimitedRedFromCr = 459;

    /// <summary>The weight of Cb in green for BT.709 limited range.</summary>
    private const int Bt709LimitedGreenFromCb = -55;

    /// <summary>The weight of Cr in green for BT.709 limited range.</summary>
    private const int Bt709LimitedGreenFromCr = -136;

    /// <summary>The weight of Cb in blue for BT.709 limited range.</summary>
    private const int Bt709LimitedBlueFromCb = 541;

    /// <summary>The weight of Cr in red for BT.601 full range.</summary>
    private const int Bt601FullRedFromCr = 359;

    /// <summary>The weight of Cb in green for BT.601 full range.</summary>
    private const int Bt601FullGreenFromCb = -88;

    /// <summary>The weight of Cr in green for BT.601 full range.</summary>
    private const int Bt601FullGreenFromCr = -183;

    /// <summary>The weight of Cb in blue for BT.601 full range.</summary>
    private const int Bt601FullBlueFromCb = 454;

    /// <summary>The weight of Cr in red for BT.709 full range.</summary>
    private const int Bt709FullRedFromCr = 403;

    /// <summary>The weight of Cb in green for BT.709 full range.</summary>
    private const int Bt709FullGreenFromCb = -48;

    /// <summary>The weight of Cr in green for BT.709 full range.</summary>
    private const int Bt709FullGreenFromCr = -120;

    /// <summary>The weight of Cb in blue for BT.709 full range.</summary>
    private const int Bt709FullBlueFromCb = 475;

    /// <summary>Gets the weights of ITU-R BT.601 for limited range video, which standard definition pictures use.</summary>
    public static ColorMatrix Bt601Limited { get; } = new(LimitedBlack, LimitedScale, Bt601LimitedRedFromCr, Bt601LimitedGreenFromCb, Bt601LimitedGreenFromCr, Bt601LimitedBlueFromCb);

    /// <summary>Gets the weights of ITU-R BT.709 for limited range video, which high definition pictures use.</summary>
    public static ColorMatrix Bt709Limited { get; } = new(LimitedBlack, LimitedScale, Bt709LimitedRedFromCr, Bt709LimitedGreenFromCb, Bt709LimitedGreenFromCr, Bt709LimitedBlueFromCb);

    /// <summary>Gets the weights of ITU-R BT.601 for full range video.</summary>
    public static ColorMatrix Bt601Full { get; } = new(0, FullScale, Bt601FullRedFromCr, Bt601FullGreenFromCb, Bt601FullGreenFromCr, Bt601FullBlueFromCb);

    /// <summary>Gets the weights of ITU-R BT.709 for full range video.</summary>
    public static ColorMatrix Bt709Full { get; } = new(0, FullScale, Bt709FullRedFromCr, Bt709FullGreenFromCb, Bt709FullGreenFromCr, Bt709FullBlueFromCb);

    /// <summary>Picks the weights a picture most likely uses when the stream does not say: BT.709 from 720 lines up, BT.601 below, both limited range.</summary>
    /// <param name="height">The height of the picture in lines.</param>
    /// <returns>The weights.</returns>
    public static ColorMatrix For(int height) => height >= HighDefinitionHeight ? Bt709Limited : Bt601Limited;
}
