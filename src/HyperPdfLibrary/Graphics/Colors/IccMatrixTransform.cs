// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// An RGB ICC profile built from /rXYZ, /gXYZ, /bXYZ colorants and /rTRC, /gTRC, /bTRC curves. Each component goes through
/// its curve, a single combined matrix takes the result from the profile's D50 XYZ to linear sRGB, and the sRGB curve
/// encodes it. The curves are tabulated for 8-bit rows and the encoding uses a lookup table.
/// </summary>
[DebuggerDisplay("IccMatrixTransform: IsSrgb={IsSrgb}")]
internal sealed class IccMatrixTransform : IccTransform
{
    /// <summary>The number of entries in the table that encodes linear values.</summary>
    private const int EncodeSteps = 16_384;

    /// <summary>The largest index of the encode table.</summary>
    private const int EncodeMaximum = EncodeSteps - 1;

    /// <summary>The largest difference from the identity matrix that still counts as sRGB.</summary>
    private const float MatrixTolerance = 0.02F;

    /// <summary>The largest difference from the sRGB curve that still counts as sRGB.</summary>
    private const float CurveTolerance = 0.01F;

    /// <summary>The offset of the shifted bit that rounds a float to a byte.</summary>
    private const float RoundHalf = 0.5F;

    /// <summary>The first test input of the sRGB curve check.</summary>
    private const float CheckLow = 0.1F;

    /// <summary>The second test input of the sRGB curve check.</summary>
    private const float CheckMiddle = 0.5F;

    /// <summary>The third test input of the sRGB curve check.</summary>
    private const float CheckHigh = 0.9F;

    /// <summary>The curve decoding sRGB values, used by the sRGB check at its test points.</summary>
    private const float SrgbLinearLimit = 0.04045F;

    /// <summary>The slope of the sRGB decode curve's straight segment.</summary>
    private const float SrgbSlope = 12.92F;

    /// <summary>The offset of the sRGB decode curve's power segment.</summary>
    private const float SrgbOffset = 0.055F;

    /// <summary>The scale of the sRGB decode curve's power segment.</summary>
    private const float SrgbScale = 1.055F;

    /// <summary>The exponent of the sRGB decode curve's power segment.</summary>
    private const float SrgbExponent = 2.4F;

    /// <summary>The sRGB byte for each of <see cref="EncodeSteps"/> linear values.</summary>
    private static readonly byte[] EncodeTable = BuildEncodeTable();

    /// <summary>The linear value of every 8-bit sample, red then green then blue; shared with the sibling.</summary>
    private readonly float[] _linear;

    /// <summary>The red, green and blue curves.</summary>
    private readonly IccCurve[] _curves;

    /// <summary>The matrix from linear profile RGB to linear sRGB.</summary>
    private readonly Matrix3 _matrix;

    /// <summary>The linear sRGB added after the matrix: zero, or the shift that black point compensation makes.</summary>
    private readonly Float3 _offset;

    /// <summary>The matrix without black point compensation, which the plain sibling uses.</summary>
    private readonly Matrix3 _plainMatrix;

    /// <summary>Whether this transform compensates for a profile black that is not the output black.</summary>
    private readonly bool _compensates;

    /// <summary>The compensating transform this plain one was made from, or <see langword="null"/> for the compensating one.</summary>
    private readonly IccMatrixTransform? _compensated;

    /// <summary>The plain sibling, made on first request, or <see langword="null"/> before that.</summary>
    private IccMatrixTransform? _plain;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccMatrixTransform"/> class that compensates for black points, as
    /// the initial graphics state asks. Compensation changes the result only when the profile's black, the colorants at
    /// zero curve output, is not XYZ zero; a plain sibling then serves renderings that turn it off.
    /// </summary>
    /// <param name="curves">The red, green and blue curves.</param>
    /// <param name="colorants">The matrix whose columns are the profile's red, green and blue colorants in D50 XYZ.</param>
    internal IccMatrixTransform(IccCurve[] curves, in Matrix3 colorants)
    {
        _curves = curves;
        _linear = new float[PdfColorSpace.RgbComponents * PixelConverter.LookupSize];
        for (var c = 0; c < curves.Length; c++)
        {
            for (var i = 0; i < PixelConverter.LookupSize; i++)
            {
                _linear[(c * PixelConverter.LookupSize) + i] = curves[c].Evaluate(i / (float)PixelConverter.MaxByte);
            }
        }

        _plainMatrix = IccProfile.XyzToSrgb.Multiply(colorants);
        var black = colorants.Transform(curves[0].Evaluate(0), curves[1].Evaluate(0), curves[^1].Evaluate(0));

        // The output is sRGB, whose black is XYZ zero.
        if (BlackPointCompensation.Between(black, default) is { } compensation)
        {
            _matrix = IccProfile.XyzToSrgb.Multiply(compensation.ScaleRows(colorants));
            _offset = IccProfile.XyzToSrgb.Transform(compensation.Offset.X, compensation.Offset.Y, compensation.Offset.Z);
            _compensates = true;
        }
        else
        {
            _matrix = _plainMatrix;
        }

        IsSrgb = IsCloseToSrgb() && IsUnshifted();
    }

    /// <summary>Initializes a new instance of the <see cref="IccMatrixTransform"/> class that does not compensate for black points.</summary>
    /// <param name="compensated">The compensating transform, whose curves and tables this one shares.</param>
    private IccMatrixTransform(IccMatrixTransform compensated)
    {
        _curves = compensated._curves;
        _linear = compensated._linear;
        _matrix = compensated._plainMatrix;
        _plainMatrix = compensated._plainMatrix;
        _compensated = compensated;
        IsSrgb = IsCloseToSrgb();
    }

    /// <inheritdoc/>
    internal override int Components => PdfColorSpace.RgbComponents;

    /// <inheritdoc/>
    internal override bool IsSrgb { get; }

    /// <summary>Gets the transform for a rendering; a matrix profile converts the same way for every intent, so only the black point choice counts.</summary>
    /// <param name="rendering">The rendering.</param>
    /// <returns>The transform with or without black point compensation.</returns>
    internal override IccTransform ForRendering(IccRendering rendering)
    {
        var compensate = rendering.Compensate && rendering.Intent != IccIntent.AbsoluteColorimetric;
        if (_compensated is not null)
        {
            return compensate ? _compensated : this;
        }

        return compensate || !_compensates ? this : Volatile.Read(ref _plain) ?? CreatePlain();
    }

    /// <inheritdoc/>
    internal override void ToRgb(ReadOnlySpan<float> components, Span<float> rgb)
    {
        var linear = _matrix.Transform(
            _curves[0].Evaluate(components[0]),
            _curves[1].Evaluate(components[1]),
            _curves[^1].Evaluate(components[^1]));
        rgb[0] = SrgbTransfer.Encode(linear.X + _offset.X);
        rgb[1] = SrgbTransfer.Encode(linear.Y + _offset.Y);
        rgb[PdfColorSpace.RgbComponents - 1] = SrgbTransfer.Encode(linear.Z + _offset.Z);
    }

    /// <inheritdoc/>
    internal override void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        var red = _linear.AsSpan(0, PixelConverter.LookupSize);
        var green = _linear.AsSpan(PixelConverter.LookupSize, PixelConverter.LookupSize);
        var blue = _linear.AsSpan(PdfColorSpace.PairSize * PixelConverter.LookupSize, PixelConverter.LookupSize);
        var previous = 0U;
        var word = 0U;
        for (var p = 0; p < pixelCount; p++)
        {
            var pixel = samples.Slice(p * PdfColorSpace.RgbComponents, PdfColorSpace.RgbComponents);
            var key = (uint)(pixel[0] | (pixel[1] << PdfColorSpace.ByteBits) | (pixel[PdfColorSpace.PairSize] << (PdfColorSpace.PairSize * PdfColorSpace.ByteBits)));
            if (p == 0 || key != previous)
            {
                var linear = _matrix.Transform(red[pixel[0]], green[pixel[1]], blue[pixel[PdfColorSpace.PairSize]]);
                word = PixelConverter.Pack(Encode(linear.X + _offset.X), Encode(linear.Y + _offset.Y), Encode(linear.Z + _offset.Z));
                previous = key;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(p * PixelConverter.BytesPerPixel)..], word);
        }
    }

    /// <summary>Builds the table that turns a linear value into an sRGB byte.</summary>
    /// <returns>The table.</returns>
    private static byte[] BuildEncodeTable()
    {
        var table = new byte[EncodeSteps];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = PixelConverter.ToByte(SrgbTransfer.Encode(i / (float)EncodeMaximum));
        }

        return table;
    }

    /// <summary>Encodes a linear value as an sRGB byte through the table.</summary>
    /// <param name="linear">The linear value.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Encode(float linear) => EncodeTable[(int)((Math.Clamp(linear, 0, 1) * EncodeMaximum) + RoundHalf)];

    /// <summary>Decodes an sRGB value to a linear one.</summary>
    /// <param name="value">The encoded value.</param>
    /// <returns>The linear value.</returns>
    private static float SrgbDecode(float value) =>
        value <= SrgbLinearLimit ? value / SrgbSlope : MathF.Pow((value + SrgbOffset) / SrgbScale, SrgbExponent);

    /// <summary>Compares a curve with the sRGB decode curve at one point.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="x">The input.</param>
    /// <returns><see langword="true"/> when they agree.</returns>
    private static bool IsNearCurve(IccCurve curve, float x) => MathF.Abs(curve.Evaluate(x) - SrgbDecode(x)) <= CurveTolerance;

    /// <summary>Determines whether a matrix entry is close to an expected value.</summary>
    /// <param name="value">The entry.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><see langword="true"/> when within the tolerance.</returns>
    private static bool IsNear(float value, float expected) => MathF.Abs(value - expected) <= MatrixTolerance;

    /// <summary>Determines whether the profile converts like sRGB: an identity matrix and the sRGB curve.</summary>
    /// <returns><see langword="true"/> when it does within a small tolerance.</returns>
    private bool IsCloseToSrgb()
    {
        var m = _matrix;
        var identity = IsNear(m.A, 1) && IsNear(m.E, 1) && IsNear(m.I, 1)
            && IsNear(m.B, 0) && IsNear(m.C, 0) && IsNear(m.D, 0) && IsNear(m.F, 0) && IsNear(m.G, 0) && IsNear(m.H, 0);
        return identity && AreCurvesSrgb();
    }

    /// <summary>Determines whether black point compensation shifts colours by no more than the sRGB tolerance.</summary>
    /// <returns><see langword="true"/> when the offset is close to zero.</returns>
    private bool IsUnshifted() => IsNear(_offset.X, 0) && IsNear(_offset.Y, 0) && IsNear(_offset.Z, 0);

    /// <summary>Makes the plain sibling and publishes it.</summary>
    /// <returns>The sibling; the same instance on every call.</returns>
    private IccMatrixTransform CreatePlain()
    {
        var created = new IccMatrixTransform(this);
        return Interlocked.CompareExchange(ref _plain, created, null) ?? created;
    }

    /// <summary>Determines whether every curve matches the sRGB decode curve at a few points.</summary>
    /// <returns><see langword="true"/> when they do.</returns>
    private bool AreCurvesSrgb()
    {
        foreach (var curve in _curves)
        {
            if (!IsNearCurve(curve, CheckLow) || !IsNearCurve(curve, CheckMiddle) || !IsNearCurve(curve, CheckHigh))
            {
                return false;
            }
        }

        return true;
    }
}
