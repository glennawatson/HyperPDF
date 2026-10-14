// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The quantization of every sub-band (ISO 15444-1 annex E): the exponent and mantissa the QCD marker carries, and the step
/// the encoder divides by. A lossless file has no quantization and only an exponent per sub-band, which sets how many
/// magnitude bit-planes the sub-band may use. A lossy file has a step per sub-band, <c>2^(Rb - exponent) * (1 + mantissa / 2^11)</c>
/// with <c>Rb</c> the sample precision plus the log2 gain of the sub-band.
/// </summary>
[DebuggerDisplay("JpxQuantPlan: {BandCount} sub-bands, reversible {Reversible}")]
internal sealed class JpxQuantPlan
{
    /// <summary>The bits of the mantissa.</summary>
    private const int MantissaBits = 11;

    /// <summary>The scale of the mantissa fraction, 2^11.</summary>
    private const double MantissaScale = 1 << MantissaBits;

    /// <summary>The sub-bands of each resolution above the lowest.</summary>
    private const int BandsPerLevel = 3;

    /// <summary>The largest exponent the five exponent bits hold.</summary>
    private const int MaxExponent = 31;

    /// <summary>The shift of the guard bits in Sqcd.</summary>
    private const int GuardShift = 5;

    /// <summary>The shift of the exponent in a one-byte SPqcd entry.</summary>
    private const int ByteExponentShift = 3;

    /// <summary>The Sqcd style of a file with no quantization.</summary>
    private const int NoQuantization = 0;

    /// <summary>The Sqcd style of a step size for every sub-band.</summary>
    private const int ScalarExpounded = 2;

    /// <summary>The log2 gain of the HH sub-band: one for each high-pass direction.</summary>
    private const int DiagonalGain = 2;

    /// <summary>The orientation of the HH sub-band, the only one with two high-pass directions.</summary>
    private const int DiagonalOrientation = 3;

    /// <summary>The orientation bit of sub-bands that are high-pass horizontally.</summary>
    private const int HorizontalHigh = 1;

    /// <summary>The orientation bit of sub-bands that are high-pass vertically.</summary>
    private const int VerticalHigh = 2;

    /// <summary>Initializes a new instance of the <see cref="JpxQuantPlan"/> class.</summary>
    /// <param name="reversible">Whether the file is lossless.</param>
    /// <param name="guardBits">The guard bits.</param>
    /// <param name="exponents">The exponent of every sub-band.</param>
    /// <param name="mantissas">The mantissa of every sub-band; zero for a lossless file.</param>
    /// <param name="precision">The sample precision the steps are relative to.</param>
    private JpxQuantPlan(bool reversible, int guardBits, int[] exponents, int[] mantissas, int precision)
    {
        Reversible = reversible;
        GuardBits = guardBits;
        Exponents = exponents;
        Mantissas = mantissas;
        Precision = precision;
    }

    /// <summary>Gets a value indicating whether the file is lossless.</summary>
    internal bool Reversible { get; }

    /// <summary>Gets the guard bits.</summary>
    internal int GuardBits { get; }

    /// <summary>Gets the exponent of every sub-band, in the order LL then the three sub-bands of each resolution.</summary>
    internal int[] Exponents { get; }

    /// <summary>Gets the mantissa of every sub-band.</summary>
    internal int[] Mantissas { get; }

    /// <summary>Gets the sample precision the steps are relative to.</summary>
    internal int Precision { get; }

    /// <summary>Gets the number of sub-bands.</summary>
    internal int BandCount => Exponents.Length;

    /// <summary>Plans the quantization.</summary>
    /// <param name="settings">The coding parameters, which hold the guard bits.</param>
    /// <returns>The plan.</returns>
    internal static JpxQuantPlan Create(JpxEncodeSettings settings)
    {
        var bands = (BandsPerLevel * settings.Levels) + 1;
        var exponents = new int[bands];
        var mantissas = new int[bands];
        for (var band = 0; band < bands; band++)
        {
            var gain = LogGain(band);
            var precision = settings.Precision + gain;
            if (settings.Reversible)
            {
                exponents[band] = Math.Min(precision + (settings.UseTransform ? 1 : 0), MaxExponent);
                continue;
            }

            var wanted = settings.Step / BandNorm(band, settings.Levels);
            var exponent = Math.Clamp(precision - (int)Math.Floor(Math.Log2(wanted)), 0, MaxExponent);
            var fraction = wanted / Math.ScaleB(1.0, precision - exponent);
            var mantissa = Math.Clamp((int)Math.Round((fraction - 1) * MantissaScale), 0, (1 << MantissaBits) - 1);
            exponents[band] = exponent;
            mantissas[band] = mantissa;
        }

        return new(settings.Reversible, settings.GuardBits, exponents, mantissas, settings.Precision);
    }

    /// <summary>Gets the position of a sub-band in the quantization order of a resolution.</summary>
    /// <param name="resolution">The resolution.</param>
    /// <param name="orientation">The orientation: 0 for LL (resolution zero only), then 1 HL, 2 LH, 3 HH.</param>
    /// <returns>The index.</returns>
    internal static int BandIndex(int resolution, int orientation) => resolution == 0 ? 0 : ((resolution - 1) * BandsPerLevel) + orientation;

    /// <summary>Gets the step a lossy file divides a sub-band's coefficients by: <c>2^(Rb - exponent) * (1 + mantissa / 2^11)</c> (equation E-3).</summary>
    /// <param name="band">The sub-band index in the quantization order.</param>
    /// <returns>The step.</returns>
    internal float GetStep(int band) =>
        (float)(Math.ScaleB(1.0, Precision + LogGain(band) - Exponents[band]) * (1 + (Mantissas[band] / MantissaScale)));

    /// <summary>Writes the Sqcd and SPqcd fields of the QCD marker.</summary>
    /// <returns>The segment after its length field.</returns>
    internal byte[] ToSegment()
    {
        var segment = new byte[1 + (BandCount * (Reversible ? 1 : sizeof(ushort)))];
        segment[0] = (byte)((GuardBits << GuardShift) | (Reversible ? NoQuantization : ScalarExpounded));
        for (var band = 0; band < BandCount; band++)
        {
            if (Reversible)
            {
                segment[1 + band] = (byte)(Exponents[band] << ByteExponentShift);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(1 + (band * sizeof(ushort))), (ushort)((Exponents[band] << MantissaBits) | Mantissas[band]));
            }
        }

        return segment;
    }

    /// <summary>Gets the log2 gain of a sub-band: 0 for LL, 1 for HL and LH, 2 for HH (table E.1).</summary>
    /// <param name="band">The sub-band index in the quantization order.</param>
    /// <returns>The log2 gain.</returns>
    private static int LogGain(int band)
    {
        if (band == 0)
        {
            return 0;
        }

        return ((band - 1) % BandsPerLevel) + 1 == DiagonalOrientation ? DiagonalGain : 1;
    }

    /// <summary>Gets the synthesis norm of a sub-band: the product of its horizontal and vertical one-dimensional norms.</summary>
    /// <param name="band">The sub-band index in the quantization order.</param>
    /// <param name="levels">The decomposition levels.</param>
    /// <returns>The norm.</returns>
    private static double BandNorm(int band, int levels)
    {
        if (levels == 0)
        {
            return 1;
        }

        if (band == 0)
        {
            return JpxSynthesisNorms.Get(false, levels) * JpxSynthesisNorms.Get(false, levels);
        }

        var level = levels - ((band - 1) / BandsPerLevel);
        var orientation = ((band - 1) % BandsPerLevel) + 1;
        return JpxSynthesisNorms.Get((orientation & HorizontalHigh) != 0, level) * JpxSynthesisNorms.Get((orientation & VerticalHigh) != 0, level);
    }
}
