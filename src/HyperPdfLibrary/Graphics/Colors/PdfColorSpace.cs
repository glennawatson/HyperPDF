// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A PDF colour space (PDF 32000 section 8.6). Parsed colour spaces are immutable and safe to use from many threads.
/// Conversion to RGB and row conversion to BGRA do not allocate.
/// </summary>
/// <remarks>
/// Row samples use one byte per component. Each byte is the component's position in the space's 8-bit default decode
/// range (<see cref="GetDefaultDecode"/> with 8 bits), scaled to 0..255; for Indexed it is the palette index.
/// </remarks>
[DebuggerDisplay("PdfColorSpace: {Kind}, {Components} components")]
public abstract class PdfColorSpace
{
    /// <summary>The most components a colour space may have.</summary>
    internal const int MaxComponents = 32;

    /// <summary>The components of an RGB colour.</summary>
    internal const int RgbComponents = 3;

    /// <summary>The bits of a full sample byte.</summary>
    internal const int ByteBits = 8;

    /// <summary>The numbers in a min/max pair.</summary>
    internal const int PairSize = 2;

    /// <summary>Initializes a new instance of the <see cref="PdfColorSpace"/> class.</summary>
    private protected PdfColorSpace()
    {
    }

    /// <summary>Gets the DeviceGray colour space.</summary>
    public static PdfColorSpace DeviceGray => DeviceGrayColorSpace.Instance;

    /// <summary>Gets the DeviceRGB colour space.</summary>
    public static PdfColorSpace DeviceRgb => DeviceRgbColorSpace.Instance;

    /// <summary>Gets the DeviceCMYK colour space.</summary>
    public static PdfColorSpace DeviceCmyk => DeviceCmykColorSpace.Instance;

    /// <summary>Gets the number of colour components.</summary>
    public abstract int Components { get; }

    /// <summary>Gets the colour space family.</summary>
    public abstract PdfColorSpaceKind Kind { get; }

    /// <summary>Gets a value indicating whether painting in this space leaves the page unchanged (Separation /None).</summary>
    public virtual bool PaintsNothing => false;

    /// <summary>Gets the names of the colorants a Separation or DeviceN space paints, one per component; <see langword="null"/> for other spaces.</summary>
    public virtual IReadOnlyList<string>? ColorantNames => null;

    /// <summary>Gets the family the content named: <see cref="Kind"/>, except for a device space that converts through the output intent profile.</summary>
    internal virtual PdfColorSpaceKind NamedKind => Kind;

    /// <summary>Gets the default decode range for 8-bit samples, as min/max pairs, built once and shared; callers must not change it.</summary>
    internal float[] ByteDecode => SampleDecode;

    /// <summary>Gets the decode range of 8-bit row samples, as min/max pairs.</summary>
    private protected float[] SampleDecode
    {
        get
        {
            var cached = Volatile.Read(ref field);
            if (cached is not null)
            {
                return cached;
            }

            var created = GetDefaultDecode(ByteBits);
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Parses a colour space, resolving names through the /ColorSpace resource dictionary.</summary>
    /// <param name="value">A name or a colour space array, already resolved.</param>
    /// <param name="colorSpaceResources">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <returns>The colour space; DeviceGray when the value is unknown or invalid.</returns>
    public static PdfColorSpace Parse(PdfValue value, PdfDictionary? colorSpaceResources) =>
        ColorSpaceParser.Parse(value, colorSpaceResources, 0) ?? DeviceGrayColorSpace.Instance;

    /// <summary>Gets the device colour space with a number of components.</summary>
    /// <param name="components">The number of components.</param>
    /// <returns>DeviceRGB for 3, DeviceCMYK for 4, otherwise DeviceGray.</returns>
    public static PdfColorSpace FromComponents(int components) => components switch
    {
        RgbComponents => DeviceRgbColorSpace.Instance,
        DeviceCmykColorSpace.ComponentCount => DeviceCmykColorSpace.Instance,
        _ => DeviceGrayColorSpace.Instance,
    };

    /// <summary>Writes the initial colour set when this space becomes current.</summary>
    /// <param name="components">Receives <see cref="Components"/> values.</param>
    public virtual void GetInitialColor(Span<float> components) => components[..Components].Clear();

    /// <summary>Converts a colour to RGB.</summary>
    /// <param name="components">The colour's components in this space.</param>
    /// <param name="rgb">Receives red, green and blue from 0 to 1.</param>
    /// <exception cref="ArgumentException">A span is too short.</exception>
    public void ToRgb(ReadOnlySpan<float> components, Span<float> rgb)
    {
        if (components.Length < Components || rgb.Length < RgbComponents)
        {
            throw new ArgumentException("The component or RGB span is too short.", nameof(components));
        }

        ToRgbCore(components, rgb);
    }

    /// <summary>Converts a row of 8-bit samples, components interleaved, to opaque BGRA.</summary>
    /// <param name="samples">The samples; see the type remarks for their scale.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    /// <exception cref="ArgumentException">A span is too short.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pixelCount"/> is negative.</exception>
    public void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pixelCount);
        if ((long)pixelCount * Components > samples.Length || (long)pixelCount * PixelConverter.BytesPerPixel > bgra.Length)
        {
            throw new ArgumentException("The sample or pixel span is too short.", nameof(samples));
        }

        ConvertRowCore(samples, bgra, pixelCount);
    }

    /// <summary>Gets the default image /Decode array for a sample depth.</summary>
    /// <param name="bitsPerComponent">The bits per component.</param>
    /// <returns>Min/max pairs, one per component.</returns>
    public virtual float[] GetDefaultDecode(int bitsPerComponent)
    {
        var decode = new float[PairSize * Components];
        for (var i = 1; i < decode.Length; i += PairSize)
        {
            decode[i] = 1;
        }

        return decode;
    }

    /// <summary>Parses a colour space using a renderer's output-intent substitutions.</summary>
    /// <param name="value">The colour space value.</param>
    /// <param name="colorSpaceResources">The colour-space resource dictionary, or null.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <returns>The colour space; DeviceGray when invalid.</returns>
    internal static PdfColorSpace Parse(PdfValue value, PdfDictionary? colorSpaceResources, OutputIntentColors? colors) =>
        ColorSpaceParser.Parse(value, colorSpaceResources, 0, colors) ?? DeviceGrayColorSpace.Instance;

    /// <summary>Builds a 256-entry BGRA table for a one-component space from its RGB conversion.</summary>
    /// <param name="space">The colour space.</param>
    /// <returns>The native-endian BGRA words, indexed by sample byte.</returns>
    internal static uint[] BuildLookup(PdfColorSpace space)
    {
        var lookup = new uint[PixelConverter.LookupSize];
        var decode = space.SampleDecode;
        Span<float> rgb = stackalloc float[RgbComponents];
        for (var i = 0; i < lookup.Length; i++)
        {
            var component = decode[0] + (i * (decode[1] - decode[0]) / PixelConverter.MaxByte);
            space.ToRgbCore([component], rgb);
            lookup[i] = PixelConverter.PackNative(rgb);
        }

        return lookup;
    }

    /// <summary>
    /// Gets the space as it converts for a rendering intent and black point choice. Only ICC conversions differ, including
    /// those an Indexed base, a Separation or DeviceN alternate or an output intent profile makes.
    /// </summary>
    /// <param name="rendering">The rendering.</param>
    /// <returns>This space, or one that converts with the rendering; the same instance on every call.</returns>
    internal virtual PdfColorSpace ForRendering(IccRendering rendering) => this;

    /// <summary>Converts a colour to RGB; the spans are long enough.</summary>
    /// <param name="components">The colour's components.</param>
    /// <param name="rgb">Receives red, green and blue from 0 to 1.</param>
    private protected abstract void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb);

    /// <summary>Converts a row of samples to BGRA; the spans are long enough.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    private protected virtual void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        ConvertRowScalar(samples, bgra, pixelCount);

    /// <summary>Converts a row one pixel at a time through <see cref="ToRgbCore"/>, reusing the result for repeated pixels.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected void ConvertRowScalar(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        ConvertRowScalar(samples, bgra, pixelCount, null);

    /// <summary>
    /// Converts a row one pixel at a time through <see cref="ToRgbCore"/>, reusing the result for repeated pixels and, for
    /// spaces of up to four components, through a cache of earlier conversions.
    /// </summary>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    /// <param name="cache">The conversion cache, or <see langword="null"/> to convert every distinct pixel.</param>
    [SkipLocalsInit]
    private protected void ConvertRowScalar(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount, ColorCache? cache)
    {
        var count = Components;
        var decode = SampleDecode;
        var useCache = cache is not null && count <= ColorCache.MaxComponents;
        Span<float> components = stackalloc float[MaxComponents];
        components = components[..count];
        Span<float> rgb = stackalloc float[RgbComponents];
        var previous = ReadOnlySpan<byte>.Empty;
        var word = 0U;
        for (var p = 0; p < pixelCount; p++)
        {
            var pixel = samples.Slice(p * count, count);
            if (p == 0 || !pixel.SequenceEqual(previous))
            {
                word = useCache ? ConvertCached(pixel, decode, components, rgb, cache!) : ConvertPixel(pixel, decode, components, rgb);
                previous = pixel;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(p * PixelConverter.BytesPerPixel)..], word);
        }
    }

    /// <summary>Maps sample bytes to component values through a decode range.</summary>
    /// <param name="pixel">The sample bytes.</param>
    /// <param name="decode">Min/max pairs.</param>
    /// <param name="components">Receives the component values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeSamples(ReadOnlySpan<byte> pixel, float[] decode, Span<float> components)
    {
        for (var i = 0; i < pixel.Length; i++)
        {
            var low = decode[PairSize * i];
            components[i] = low + (pixel[i] * (decode[(PairSize * i) + 1] - low) / PixelConverter.MaxByte);
        }
    }

    /// <summary>Converts one pixel to an opaque BGRA word.</summary>
    /// <param name="pixel">The sample bytes.</param>
    /// <param name="decode">Min/max pairs.</param>
    /// <param name="components">Scratch for the component values.</param>
    /// <param name="rgb">Scratch for the RGB values.</param>
    /// <returns>The BGRA word.</returns>
    private uint ConvertPixel(ReadOnlySpan<byte> pixel, float[] decode, Span<float> components, Span<float> rgb)
    {
        DecodeSamples(pixel, decode, components);
        ToRgbCore(components, rgb);
        return PixelConverter.Pack(PixelConverter.ToByte(rgb[0]), PixelConverter.ToByte(rgb[1]), PixelConverter.ToByte(rgb[RgbComponents - 1]));
    }

    /// <summary>Converts one pixel through the cache.</summary>
    /// <param name="pixel">The sample bytes.</param>
    /// <param name="decode">Min/max pairs.</param>
    /// <param name="components">Scratch for the component values.</param>
    /// <param name="rgb">Scratch for the RGB values.</param>
    /// <param name="cache">The conversion cache.</param>
    /// <returns>The BGRA word.</returns>
    private uint ConvertCached(ReadOnlySpan<byte> pixel, float[] decode, Span<float> components, Span<float> rgb, ColorCache cache)
    {
        var key = ColorCache.Pack(pixel);
        if (cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var word = ConvertPixel(pixel, decode, components, rgb);
        cache.Set(key, word);
        return word;
    }
}
