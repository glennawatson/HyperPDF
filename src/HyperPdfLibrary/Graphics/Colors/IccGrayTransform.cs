// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A gray ICC profile with a /kTRC curve. The curve gives the luminance, whose D50 white maps to sRGB white, so the result
/// is the luminance encoded with the sRGB curve. A 256-entry table converts rows.
/// </summary>
internal sealed class IccGrayTransform : IccTransform
{
    /// <summary>The curve from gray to linear luminance.</summary>
    private readonly IccCurve _curve;

    /// <summary>The BGRA word for every 8-bit gray, in native byte order.</summary>
    private readonly uint[] _lookup = new uint[PixelConverter.LookupSize];

    /// <summary>The factor applied to the luminance: 1, or the stretch black point compensation makes.</summary>
    private readonly float _scale;

    /// <summary>The amount added to the luminance after scaling.</summary>
    private readonly float _offset;

    /// <summary>Whether this transform compensates for a curve that does not start at zero.</summary>
    private readonly bool _compensates;

    /// <summary>The compensating transform this plain one was made from, or <see langword="null"/> for the compensating one.</summary>
    private readonly IccGrayTransform? _compensated;

    /// <summary>The plain sibling, made on first request, or <see langword="null"/> before that.</summary>
    private IccGrayTransform? _plain;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccGrayTransform"/> class that compensates for black points, as the
    /// initial graphics state asks. Compensation changes the result only when the curve does not start at zero; a plain
    /// sibling then serves renderings that turn it off.
    /// </summary>
    /// <param name="curve">The gray tone curve.</param>
    internal IccGrayTransform(IccCurve curve)
    {
        _curve = curve;
        _scale = 1;
        var black = curve.Evaluate(0);

        // The output is sRGB, whose black is XYZ zero; a gray black point lies on the white point's axis.
        if (BlackPointCompensation.Between(new(IccPcs.WhiteX * black, IccPcs.WhiteY * black, IccPcs.WhiteZ * black), default) is { } compensation)
        {
            _scale = compensation.Scale.Y;
            _offset = compensation.Offset.Y;
            _compensates = true;
        }

        FillLookup();
    }

    /// <summary>Initializes a new instance of the <see cref="IccGrayTransform"/> class that does not compensate for black points.</summary>
    /// <param name="compensated">The compensating transform, whose curve this one shares.</param>
    private IccGrayTransform(IccGrayTransform compensated)
    {
        _curve = compensated._curve;
        _scale = 1;
        _compensated = compensated;
        FillLookup();
    }

    /// <inheritdoc/>
    internal override int Components => 1;

    /// <summary>Gets the transform for a rendering; a gray curve converts the same way for every intent, so only the black point choice counts.</summary>
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
    internal override void ToRgb(ReadOnlySpan<float> components, Span<float> rgb) =>
        rgb[..PdfColorSpace.RgbComponents].Fill(SrgbTransfer.Encode((_scale * _curve.Evaluate(components[0])) + _offset));

    /// <inheritdoc/>
    internal override void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);

    /// <summary>Makes the plain sibling and publishes it.</summary>
    /// <returns>The sibling; the same instance on every call.</returns>
    private IccGrayTransform CreatePlain()
    {
        var created = new IccGrayTransform(this);
        return Interlocked.CompareExchange(ref _plain, created, null) ?? created;
    }

    /// <summary>Fills the table of BGRA words.</summary>
    private void FillLookup()
    {
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        for (var i = 0; i < _lookup.Length; i++)
        {
            ToRgb([i / (float)PixelConverter.MaxByte], rgb);
            _lookup[i] = PixelConverter.PackNative(rgb);
        }
    }
}
