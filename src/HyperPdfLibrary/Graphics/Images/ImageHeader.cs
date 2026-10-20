// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>The entries of an image dictionary that decoding needs, read once and checked.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="BitsPerComponent">The bits per component: 1, 2, 4, 8 or 16.</param>
/// <param name="IsStencil">Whether this is a stencil image mask.</param>
/// <param name="Interpolate">Whether the image asks for smoothing.</param>
/// <param name="ColorSpace">The colour space, or <see langword="null"/> when missing; stencil masks have none.</param>
/// <param name="Decode">The /Decode array, or <see langword="null"/> when missing or the wrong length.</param>
/// <param name="ColorKey">The colour-key /Mask ranges in raw sample values, or <see langword="null"/>.</param>
internal readonly record struct ImageHeader(
    int Width,
    int Height,
    int BitsPerComponent,
    bool IsStencil,
    bool Interpolate,
    PdfColorSpace? ColorSpace,
    float[]? Decode,
    int[]? ColorKey)
{
    /// <summary>The largest number of pixels decoded, 16384 squared.</summary>
    internal const long MaxPixels = 16_384L * 16_384L;

    /// <summary>The most bytes of decoded pixels, or of source samples, one image may need (512 MB).</summary>
    internal const long MaxDecodedBytes = 512L * 1024 * 1024;

    /// <summary>The bits per component assumed when /BitsPerComponent is missing.</summary>
    internal const int DefaultBits = 8;

    /// <summary>The widest supported sample.</summary>
    internal const int MaxBits = 16;

    /// <summary>The bytes in one decoded BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The bits of a stencil mask sample.</summary>
    private const int StencilBits = 1;

    /// <summary>
    /// Gets a value indicating whether the decoder may give a one-component image as 8-bit gray pixels instead of BGRA.
    /// Set only for images drawn directly, never for ones that get a mask or colour key afterwards.
    /// </summary>
    internal bool Compact { get; init; }

    /// <summary>
    /// Gets the halvings the decoder averages into the pixels while converting rows, so the full-size image is never held.
    /// Set only for opaque images that get no mask or colour key afterwards; zero decodes in full.
    /// </summary>
    internal int BoxLevels { get; init; }

    /// <summary>Gets the number of components per pixel.</summary>
    internal int Components => IsStencil ? 1 : (ColorSpace?.Components ?? 1);

    /// <summary>Gets the number of bytes in one row of samples.</summary>
    internal int RowBytes => (int)((((long)Width * Components * BitsPerComponent) + DefaultBits - 1) / DefaultBits);

    /// <summary>Gets the supported bits per component.</summary>
    private static ReadOnlySpan<byte> ValidBits => [0x01, 0x02, 0x04, 0x08, 0x10];

    /// <summary>Gets the decode range of a stencil mask or an image with no colour space: 0 to 1.</summary>
    private static float[] StencilDecode { get; } = [0, 1];

    /// <summary>Reads the header of an image XObject.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The header, or <see langword="null"/> when the size or depth is invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImageHeader? FromXObject(PdfDictionary dictionary) => Read(dictionary, null, false, null);

    /// <summary>Reads the header of an image XObject whose colour space may name a page or form resource.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <returns>The header, or <see langword="null"/> when the size or depth is invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImageHeader? FromXObject(PdfDictionary dictionary, PdfDictionary? colorSpaces) => Read(dictionary, colorSpaces, false, null);

    /// <summary>Reads an image XObject with an explicit output-intent colour selection.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="colorSpaces">The colour-space resource dictionary, or null.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <returns>The header, or null when invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImageHeader? FromXObject(PdfDictionary dictionary, PdfDictionary? colorSpaces, OutputIntentColors? colors) =>
        Read(dictionary, colorSpaces, false, colors);

    /// <summary>Reads the header of an inline image, which may use abbreviated keys and name a resource colour space.</summary>
    /// <param name="dictionary">The inline image dictionary.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <returns>The header, or <see langword="null"/> when the size or depth is invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImageHeader? FromInline(PdfDictionary dictionary, PdfDictionary? colorSpaces) => Read(dictionary, colorSpaces, true, null);

    /// <summary>Reads an inline image with an explicit output-intent colour selection.</summary>
    /// <param name="dictionary">The inline image dictionary.</param>
    /// <param name="colorSpaces">The colour-space resource dictionary, or null.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <returns>The header, or null when invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ImageHeader? FromInline(PdfDictionary dictionary, PdfDictionary? colorSpaces, OutputIntentColors? colors) =>
        Read(dictionary, colorSpaces, true, colors);

    /// <summary>
    /// Gets a value by its inline abbreviation, or by its full key. When an inline image has both, the abbreviated key takes
    /// precedence (ISO 32000-2 §8.9.7).
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The full key.</param>
    /// <param name="abbreviation">The inline abbreviation.</param>
    /// <param name="inline">Whether abbreviations are allowed.</param>
    /// <returns>The value, or null.</returns>
    internal static PdfValue Get(PdfDictionary dictionary, KnownName key, KnownName abbreviation, bool inline)
    {
        var abbreviated = inline ? dictionary.Get(abbreviation) : default;
        return abbreviated.IsNull ? dictionary.Get(key) : abbreviated;
    }

    /// <summary>Gets the decode range of each component: the /Decode array or the colour space's default.</summary>
    /// <returns>Min/max pairs, one per component. The array may be shared, so callers must not change it.</returns>
    internal float[] GetDecode()
    {
        if (Decode is not null)
        {
            return Decode;
        }

        if (IsStencil || ColorSpace is null)
        {
            return StencilDecode;
        }

        return BitsPerComponent == DefaultBits ? ColorSpace.ByteDecode : ColorSpace.GetDefaultDecode(BitsPerComponent);
    }

    /// <summary>Reads and checks an image dictionary.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="inline">Whether this is an inline image.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <returns>The header, or <see langword="null"/>.</returns>
    private static ImageHeader? Read(PdfDictionary dictionary, PdfDictionary? colorSpaces, bool inline, OutputIntentColors? colors)
    {
        var width = Get(dictionary, KnownName.Width, KnownName.W, inline).AsInt32();
        var height = Get(dictionary, KnownName.Height, KnownName.H, inline).AsInt32();
        var isStencil = Get(dictionary, KnownName.ImageMask, KnownName.IM, inline).AsBoolean();
        var bits = isStencil ? StencilBits : Get(dictionary, KnownName.BitsPerComponent, KnownName.BPC, inline).AsInt32(DefaultBits);
        var valid = width > 0 && height > 0 && (long)width * height * PixelBytes <= MaxDecodedBytes && bits is > 0 and <= MaxBits && ValidBits.Contains((byte)bits);
        if (!valid)
        {
            return null;
        }

        var header = ReadRest(dictionary, colorSpaces, inline, colors, new(width, height, bits, isStencil, false, null, null, null));
        return IsAffordable(header) ? header : null;
    }

    /// <summary>Checks that the source samples and one unpacked row fit the byte budget, using long arithmetic.</summary>
    /// <param name="header">The completed header.</param>
    /// <returns><see langword="true"/> when the sizes are within budget.</returns>
    private static bool IsAffordable(in ImageHeader header)
    {
        var rowBits = (long)header.Width * header.Components * header.BitsPerComponent;
        var rowBytes = (rowBits + DefaultBits - 1) / DefaultBits;
        return rowBytes * header.Height <= MaxDecodedBytes && (long)header.Width * header.Components <= MaxDecodedBytes;
    }

    /// <summary>Reads the colour space, decode array, colour key and interpolation flag.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="inline">Whether this is an inline image.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="header">The header with its size and depth.</param>
    /// <returns>The completed header.</returns>
    private static ImageHeader ReadRest(PdfDictionary dictionary, PdfDictionary? colorSpaces, bool inline, OutputIntentColors? colors, in ImageHeader header)
    {
        var colorSpace = header.IsStencil ? null : ColorSpaceParser.Parse(Get(dictionary, KnownName.ColorSpace, KnownName.CS, inline), colorSpaces, 0, colors);
        var components = header.IsStencil ? 1 : colorSpace?.Components ?? 1;
        return header with
        {
            ColorSpace = colorSpace,
            Interpolate = Get(dictionary, KnownName.Interpolate, KnownName.I, inline).AsBoolean(),
            Decode = ReadDecode(Get(dictionary, KnownName.Decode, KnownName.D, inline).AsArray(), components),
            ColorKey = inline || header.IsStencil ? null : ReadColorKey(dictionary.Get(KnownName.Mask).AsArray(), components),
        };
    }

    /// <summary>Reads a /Decode array of one pair per component.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="components">The components per pixel.</param>
    /// <returns>The pairs, or <see langword="null"/> when missing or the wrong length.</returns>
    private static float[]? ReadDecode(PdfArray? array, int components)
    {
        if (array is null || array.Count < PdfColorSpace.PairSize * components)
        {
            return null;
        }

        var decode = new float[PdfColorSpace.PairSize * components];
        return array.ReadNumbers(decode) == decode.Length ? decode : null;
    }

    /// <summary>Reads a colour-key /Mask array of one min/max pair per component.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="components">The components per pixel.</param>
    /// <returns>The pairs, or <see langword="null"/> when missing or the wrong length.</returns>
    private static int[]? ReadColorKey(PdfArray? array, int components)
    {
        if (array is null || array.Count < PdfColorSpace.PairSize * components)
        {
            return null;
        }

        var key = new int[PdfColorSpace.PairSize * components];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = array.GetInt32(i);
        }

        return key;
    }
}
