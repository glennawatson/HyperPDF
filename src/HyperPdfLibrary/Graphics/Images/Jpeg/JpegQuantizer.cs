// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The quantization tables of an encoded JPEG: the example tables of ISO/IEC 10918-1 Annex K.1, scaled for a quality from
/// 1 to 100, and the reciprocals the forward transform multiplies by.
/// </summary>
internal static class JpegQuantizer
{
    /// <summary>The lowest quality.</summary>
    internal const int MinQuality = 1;

    /// <summary>The highest quality.</summary>
    internal const int MaxQuality = 100;

    /// <summary>The fixed-point bits of a reciprocal.</summary>
    internal const int ReciprocalBits = 18;

    /// <summary>The quality at which the Annex K tables are used unscaled.</summary>
    private const int MiddleQuality = 50;

    /// <summary>The scale percentage at the middle quality.</summary>
    private const int FullScale = 100;

    /// <summary>The numerator of the scale below the middle quality.</summary>
    private const int LowQualityNumerator = 5000;

    /// <summary>The offset of the scale from the middle quality upward.</summary>
    private const int HighQualityBase = 200;

    /// <summary>The factor of the scale from the middle quality upward.</summary>
    private const int HighQualityFactor = 2;

    /// <summary>The largest step a baseline table may hold.</summary>
    private const int MaxStep = 255;

    /// <summary>The fixed-point bits that a step carries in its reciprocal: the transform leaves two fraction bits.</summary>
    private const int TransformFractionBits = 2;

    /// <summary>Gets the luminance table of Annex K.1 in zigzag order.</summary>
    private static ReadOnlySpan<byte> LuminanceBase =>
    [
        0x10, 0x0B, 0x0C, 0x0E, 0x0C, 0x0A, 0x10, 0x0E, 0x0D, 0x0E, 0x12, 0x11, 0x10, 0x13, 0x18, 0x28,
        0x1A, 0x18, 0x16, 0x16, 0x18, 0x31, 0x23, 0x25, 0x1D, 0x28, 0x3A, 0x33, 0x3D, 0x3C, 0x39, 0x33,
        0x38, 0x37, 0x40, 0x48, 0x5C, 0x4E, 0x40, 0x44, 0x57, 0x45, 0x37, 0x38, 0x50, 0x6D, 0x51, 0x57,
        0x5F, 0x62, 0x67, 0x68, 0x67, 0x3E, 0x4D, 0x71, 0x79, 0x70, 0x64, 0x78, 0x5C, 0x65, 0x67, 0x63,
    ];

    /// <summary>Gets the chrominance table of Annex K.1 in zigzag order.</summary>
    private static ReadOnlySpan<byte> ChrominanceBase =>
    [
        0x11, 0x12, 0x12, 0x18, 0x15, 0x18, 0x2F, 0x1A, 0x1A, 0x2F, 0x63, 0x42, 0x38, 0x42, 0x63, 0x63,
        0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63,
        0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63,
        0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63, 0x63,
    ];

    /// <summary>Scales a table of Annex K.1 for a quality, as the Independent JPEG Group's scale does, so the steps match other encoders.</summary>
    /// <param name="chrominance">Whether to scale the chrominance table instead of the luminance table.</param>
    /// <param name="quality">The quality, clamped to 1 through 100.</param>
    /// <param name="zigzag">Receives the 64 steps in zigzag order, each from 1 to 255.</param>
    internal static void Scale(bool chrominance, int quality, Span<byte> zigzag)
    {
        var clamped = Math.Clamp(quality, MinQuality, MaxQuality);
        var scale = clamped < MiddleQuality ? LowQualityNumerator / clamped : HighQualityBase - (clamped * HighQualityFactor);
        var table = chrominance ? ChrominanceBase : LuminanceBase;
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            var step = ((table[i] * scale) + (FullScale / HighQualityFactor)) / FullScale;
            zigzag[i] = (byte)Math.Clamp(step, 1, MaxStep);
        }
    }

    /// <summary>Builds the fixed-point reciprocals of a table, laid out like the forward transform's output.</summary>
    /// <param name="zigzag">The 64 steps in zigzag order.</param>
    /// <param name="reciprocals">Receives 64 reciprocals in storage order, scaled by 2 to the power <see cref="ReciprocalBits"/> over four.</param>
    internal static void BuildReciprocals(ReadOnlySpan<byte> zigzag, Span<int> reciprocals)
    {
        var storage = JpegBlock.Zigzag;
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            var step = zigzag[i];
            reciprocals[storage[i]] = ((1 << (ReciprocalBits - TransformFractionBits)) + (step / HighQualityFactor)) / step;
        }
    }
}
