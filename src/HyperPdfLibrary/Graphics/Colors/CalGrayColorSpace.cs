// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The CalGray colour space (ISO 32000-2 8.6.5.2): the gray value A is raised to /Gamma to give the luminance Y, which is
/// neutral at any white point, so the result is the sRGB encoding of Y. A row converts through a 256-entry lookup table.
/// The /BlackPoint is not used.
/// </summary>
internal sealed class CalGrayColorSpace : PdfColorSpace
{
    /// <summary>The gamma of a space without a valid /Gamma.</summary>
    private const float DefaultGamma = 1F;

    /// <summary>The BGRA word for every byte sample.</summary>
    private readonly uint[] _lookup = new uint[PixelConverter.LookupSize];

    /// <summary>The exponent applied to the gray value.</summary>
    private readonly float _gamma;

    /// <summary>Initializes a new instance of the <see cref="CalGrayColorSpace"/> class.</summary>
    /// <param name="gamma">The /Gamma; a value that is not positive and finite counts as 1.</param>
    internal CalGrayColorSpace(float gamma)
    {
        _gamma = float.IsFinite(gamma) && gamma > 0 ? gamma : DefaultGamma;
        Span<float> rgb = stackalloc float[RgbComponents];
        for (var i = 0; i < _lookup.Length; i++)
        {
            rgb.Fill(Encode(i / (float)PixelConverter.MaxByte));
            _lookup[i] = PixelConverter.PackNative(rgb);
        }
    }

    /// <inheritdoc/>
    public override int Components => 1;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.CalGray;

    /// <summary>Parses a CalGray dictionary.</summary>
    /// <param name="dictionary">The dictionary, or <see langword="null"/>.</param>
    /// <returns>The colour space.</returns>
    internal static CalGrayColorSpace Parse(PdfDictionary? dictionary)
    {
        var gamma = dictionary?.Get(KnownName.Gamma) is { IsNumber: true } value ? value.AsSingle() : DefaultGamma;
        return new(gamma);
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) =>
        rgb[..RgbComponents].Fill(Encode(components[0]));

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);

    /// <summary>Converts a gray value to the sRGB value of its luminance.</summary>
    /// <param name="gray">The gray value; it is clamped to 0..1.</param>
    /// <returns>The sRGB value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Encode(float gray) => SrgbTransfer.Encode(MathF.Pow(Math.Clamp(gray, 0, 1), _gamma));
}
