// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Makes the colour space of the CIELab enumerated colour space of a JPX file (ISO 15444-2 M.11.7.3.1, EnumCS 14). A code value
/// N of a channel of n bits is L* = (N - O) * R / (2^n - 1) (M.11.7.4.1, equation M-18). The result is a PDF Lab space whose
/// range matches, so the 8-bit samples the decoder writes convert like any other Lab image.
/// </summary>
internal static class JpxLabSpace
{
    /// <summary>The components of a Lab space.</summary>
    private const int Components = 3;

    /// <summary>The default L* range.</summary>
    private const uint DefaultRangeL = 100;

    /// <summary>The default a* range.</summary>
    private const uint DefaultRangeA = 170;

    /// <summary>The default b* range.</summary>
    private const uint DefaultRangeB = 200;

    /// <summary>The widest channel read as Lab.</summary>
    private const int MaxBits = 16;

    /// <summary>The bit-depth below which the default b* offset is not defined.</summary>
    private const int MinBitsForB = 3;

    /// <summary>The second shift of the default b* offset.</summary>
    private const int BOffsetLowShift = 3;

    /// <summary>The first shift of the default b* offset.</summary>
    private const int BOffsetHighShift = 2;

    /// <summary>The illuminant code of CIE D50, the default.</summary>
    private const uint IlluminantD50 = 0x00443530;

    /// <summary>The illuminant code of CIE D65.</summary>
    private const uint IlluminantD65 = 0x00443635;

    /// <summary>The illuminant code of CIE D75.</summary>
    private const uint IlluminantD75 = 0x00443735;

    /// <summary>The illuminant code of CIE A (SA).</summary>
    private const uint IlluminantA = 0x00005341;

    /// <summary>The illuminant code of CIE C (SC).</summary>
    private const uint IlluminantC = 0x00005343;

    /// <summary>The illuminant code of CIE F2.</summary>
    private const uint IlluminantF2 = 0x00004632;

    /// <summary>The illuminant code of CIE F7.</summary>
    private const uint IlluminantF7 = 0x00004637;

    /// <summary>The illuminant code of CIE F11.</summary>
    private const uint IlluminantF11 = 0x00463131;

    /// <summary>The X of CIE D50 (Y is 1).</summary>
    private const float D50X = 0.96422F;

    /// <summary>The Z of CIE D50.</summary>
    private const float D50Z = 0.82521F;

    /// <summary>The X of CIE D65.</summary>
    private const float D65X = 0.95047F;

    /// <summary>The Z of CIE D65.</summary>
    private const float D65Z = 1.08883F;

    /// <summary>The X of CIE D75.</summary>
    private const float D75X = 0.94972F;

    /// <summary>The Z of CIE D75.</summary>
    private const float D75Z = 1.22638F;

    /// <summary>The X of CIE A.</summary>
    private const float AX = 1.0985F;

    /// <summary>The Z of CIE A.</summary>
    private const float AZ = 0.35585F;

    /// <summary>The X of CIE C.</summary>
    private const float CX = 0.98074F;

    /// <summary>The Z of CIE C.</summary>
    private const float CZ = 1.18232F;

    /// <summary>The X of CIE F2.</summary>
    private const float F2X = 0.99186F;

    /// <summary>The Z of CIE F2.</summary>
    private const float F2Z = 0.67393F;

    /// <summary>The X of CIE F7.</summary>
    private const float F7X = 0.95041F;

    /// <summary>The Z of CIE F7.</summary>
    private const float F7Z = 1.08747F;

    /// <summary>The X of CIE F11.</summary>
    private const float F11X = 1.00962F;

    /// <summary>The Z of CIE F11.</summary>
    private const float F11Z = 0.6435F;

    /// <summary>Makes the colour space.</summary>
    /// <param name="parameters">The EP field, or <see langword="null"/> for the defaults.</param>
    /// <param name="channels">The channels in colour order; the first three are L*, a* and b*.</param>
    /// <returns>
    /// The colour space, or <see langword="null"/> when the channels or parameters cannot be read as Lab: signed or too deep
    /// channels, an L* range other than 0 to 100, or an a* or b* range of zero.
    /// </returns>
    internal static PdfColorSpace? Create(JpxLabParameters? parameters, JpxChannel[] channels)
    {
        if (channels.Length < Components || !IsUsable(channels))
        {
            return null;
        }

        var values = parameters ?? Defaults(channels[1].Precision, channels[2].Precision);
        if (values.RangeL != DefaultRangeL || values.OffsetL != 0 || values.RangeA == 0 || values.RangeB == 0)
        {
            return null;
        }

        var (x, z) = WhitePoint(values.Illuminant);
        var a = Span(values.RangeA, values.OffsetA, channels[1].Precision);
        var b = Span(values.RangeB, values.OffsetB, channels[2].Precision);
        return new LabColorSpace([a.Min, a.Max, b.Min, b.Max], new(x, 1, z));
    }

    /// <summary>Gets the defaults of the T.42 encoding (M.11.7.4.1): the ranges 100, 170 and 200, and the offsets for the bit depths.</summary>
    /// <param name="bitsA">The bits of the a* channel.</param>
    /// <param name="bitsB">The bits of the b* channel.</param>
    /// <returns>The parameters, with illuminant D50.</returns>
    private static JpxLabParameters Defaults(int bitsA, int bitsB)
    {
        var offsetB = bitsB >= MinBitsForB ? (1U << (bitsB - BOffsetHighShift)) + (1U << (bitsB - BOffsetLowShift)) : 0;
        return new(DefaultRangeL, 0, DefaultRangeA, 1U << (bitsA - 1), DefaultRangeB, offsetB, IlluminantD50);
    }

    /// <summary>Determines whether the first three channels are unsigned and of a depth that can be read.</summary>
    /// <param name="channels">The channels.</param>
    /// <returns><see langword="true"/> when they can be.</returns>
    private static bool IsUsable(JpxChannel[] channels)
    {
        for (var i = 0; i < Components; i++)
        {
            if (channels[i].Signed || channels[i].Precision is < 1 or > MaxBits)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the lowest and highest value of a channel from its range and offset.</summary>
    /// <param name="range">R.</param>
    /// <param name="offset">O.</param>
    /// <param name="bits">The channel's bits.</param>
    /// <returns>The values the lowest and highest code values give.</returns>
    private static ValueSpan Span(uint range, uint offset, int bits)
    {
        var min = -(float)offset * range / ((1 << bits) - 1);
        return new(min, min + range);
    }

    /// <summary>Gets the white point of an illuminant, as X and Z with Y of 1.</summary>
    /// <param name="illuminant">The IL field.</param>
    /// <returns>X and Z; D50 for an illuminant that is not in the table, such as a colour temperature.</returns>
    private static WhiteXz WhitePoint(uint illuminant) => illuminant switch
    {
        IlluminantD65 => new(D65X, D65Z),
        IlluminantD75 => new(D75X, D75Z),
        IlluminantA => new(AX, AZ),
        IlluminantC => new(CX, CZ),
        IlluminantF2 => new(F2X, F2Z),
        IlluminantF7 => new(F7X, F7Z),
        IlluminantF11 => new(F11X, F11Z),
        _ => new(D50X, D50Z),
    };

    /// <summary>The lowest and highest value of a channel.</summary>
    /// <param name="Min">The lowest.</param>
    /// <param name="Max">The highest.</param>
    private readonly record struct ValueSpan(float Min, float Max);

    /// <summary>The X and Z of a white point.</summary>
    /// <param name="X">X.</param>
    /// <param name="Z">Z.</param>
    private readonly record struct WhiteXz(float X, float Z);
}
