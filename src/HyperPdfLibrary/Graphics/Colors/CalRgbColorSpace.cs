// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The CalRGB colour space (ISO 32000-2 8.6.5.3): each component is raised to its /Gamma, the /Matrix turns the result
/// into XYZ, and XYZ converts to sRGB using the /WhitePoint. The /BlackPoint is not used. A row converts through a cache of earlier
/// conversions, because the maths needs powers.
/// </summary>
internal sealed class CalRgbColorSpace : PdfColorSpace
{
    /// <summary>The numbers in a /Matrix.</summary>
    private const int MatrixLength = 9;

    /// <summary>The gamma per component.</summary>
    private readonly float[] _gamma;

    /// <summary>The matrix from calibrated components to XYZ.</summary>
    private readonly Matrix3 _matrix;

    /// <summary>The XYZ to sRGB conversion for the white point.</summary>
    private readonly WhitePointConverter _converter;

    /// <summary>The cache of earlier row conversions.</summary>
    private readonly ColorCache _cache = new();

    /// <summary>Initializes a new instance of the <see cref="CalRgbColorSpace"/> class.</summary>
    /// <param name="gamma">The gamma per component.</param>
    /// <param name="matrix">The matrix from components to XYZ.</param>
    /// <param name="converter">The XYZ to sRGB conversion.</param>
    internal CalRgbColorSpace(float[] gamma, in Matrix3 matrix, WhitePointConverter converter)
    {
        _gamma = gamma;
        _matrix = matrix;
        _converter = converter;
    }

    /// <inheritdoc/>
    public override int Components => RgbComponents;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.CalRgb;

    /// <summary>Parses a CalRGB dictionary.</summary>
    /// <param name="dictionary">The dictionary, or <see langword="null"/>.</param>
    /// <returns>The colour space.</returns>
    internal static CalRgbColorSpace Parse(PdfDictionary? dictionary)
    {
        float[] gamma = [1, 1, 1];
        _ = dictionary?.GetArray(KnownName.Gamma)?.ReadNumbers(gamma);

        // A matrix with fewer than nine numbers is ignored, so the components are used as XYZ.
        Span<float> read = stackalloc float[MatrixLength];
        var matrix = new Matrix3(1, 0, 0, 0, 1, 0, 0, 0, 1);
        if (dictionary?.GetArray(KnownName.Matrix)?.ReadNumbers(read) == MatrixLength)
        {
            matrix = Matrix3.FromColumns(read);
        }

        return new(gamma, matrix, WhitePointConverter.FromDictionary(dictionary));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        var a = MathF.Pow(Math.Clamp(components[0], 0, 1), _gamma[0]);
        var b = MathF.Pow(Math.Clamp(components[1], 0, 1), _gamma[1]);
        var c = MathF.Pow(Math.Clamp(components[2], 0, 1), _gamma[^1]);
        var xyz = _matrix.Transform(a, b, c);
        _converter.ToRgb(xyz.X, xyz.Y, xyz.Z, rgb);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        ConvertRowScalar(samples, bgra, pixelCount, _cache);
}
