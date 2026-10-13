// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// A fill or stroke colour, resolved to RGB when it is set so painting never converts colour. A pattern colour keeps the
/// pattern too, and its RGB is the underlying colour of an uncoloured pattern.
/// </summary>
/// <param name="Space">The colour space.</param>
/// <param name="Rgb">The colour as 0xRRGGBB.</param>
/// <param name="Pattern">The pattern, when the space is a pattern space and a pattern is selected.</param>
/// <param name="PaintsNothing">Whether the colour marks nothing, as the Separation colourant /None does.</param>
[DebuggerDisplay("ColorState: {Rgb:X6}")]
public readonly record struct ColorState(PdfColorSpace Space, uint Rgb, PdfPatternPaint? Pattern, bool PaintsNothing)
{
    /// <summary>The most components a colour keeps so it can be converted again when the rendering intent changes.</summary>
    internal const int SavedComponents = 4;

    /// <summary>The bits per RGB channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The largest channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The shift of the red channel.</summary>
    private const int RedShift = 16;

    /// <summary>The position of blue in an RGB triple.</summary>
    private const int BlueIndex = 2;

    /// <summary>Gets black in DeviceGray, the initial colour.</summary>
    public static ColorState Black => new(PdfColorSpace.DeviceGray, 0, null, false);

    /// <summary>Gets the red channel, 0 to 255.</summary>
    public byte Red => (byte)(Rgb >>> RedShift);

    /// <summary>Gets the green channel, 0 to 255.</summary>
    public byte Green => (byte)(Rgb >>> ChannelBits);

    /// <summary>Gets the blue channel, 0 to 255.</summary>
    public byte Blue => (byte)Rgb;

    /// <summary>Gets the first <see cref="SavedComponents"/> components the colour was resolved from; unused ones are zero.</summary>
    internal Vector4 Components { get; init; }

    /// <summary>Resolves components in a colour space, converting them as the space itself does.</summary>
    /// <param name="space">The colour space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorState Resolve(PdfColorSpace space, ReadOnlySpan<float> components) => Create(space, space, components);

    /// <summary>Packs RGB components into 0xRRGGBB.</summary>
    /// <param name="rgb">Red, green and blue from 0 to 1.</param>
    /// <returns>The packed colour.</returns>
    public static uint Pack(ReadOnlySpan<float> rgb) =>
        (Channel(rgb[0]) << RedShift) | (Channel(rgb[1]) << ChannelBits) | Channel(rgb[BlueIndex]);

    /// <summary>Resolves components in a colour space, converting with a rendering intent and black point choice.</summary>
    /// <param name="space">The colour space the colour keeps.</param>
    /// <param name="components">The components.</param>
    /// <param name="rendering">The rendering ICC conversions use.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ColorState Resolve(PdfColorSpace space, ReadOnlySpan<float> components, IccRendering rendering) =>
        Create(space, space.ForRendering(rendering), components);

    /// <summary>Converts the colour again for another rendering, when ICC conversion makes it differ.</summary>
    /// <param name="previous">The rendering the colour was resolved with.</param>
    /// <param name="rendering">The new rendering.</param>
    /// <returns>The colour for the new rendering; this colour when the rendering does not change it or its components were not kept.</returns>
    internal ColorState Rerender(IccRendering previous, IccRendering rendering)
    {
        if (Pattern is not null || Space.Components > SavedComponents || ReferenceEquals(Space.ForRendering(previous), Space.ForRendering(rendering)))
        {
            return this;
        }

        Span<float> components = stackalloc float[SavedComponents];
        Components.CopyTo(components);
        return Resolve(Space, components, rendering) with { PaintsNothing = PaintsNothing };
    }

    /// <summary>Makes a colour that keeps one space and converts through another.</summary>
    /// <param name="space">The colour space the colour keeps.</param>
    /// <param name="converting">The space that converts the components: the kept one, or its variant for a rendering.</param>
    /// <param name="components">The components.</param>
    /// <returns>The colour.</returns>
    private static ColorState Create(PdfColorSpace space, PdfColorSpace converting, ReadOnlySpan<float> components)
    {
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        converting.ToRgb(components, rgb);
        return new(space, Pack(rgb), null, space.PaintsNothing) { Components = Save(components, space.Components) };
    }

    /// <summary>Keeps the first components of a colour.</summary>
    /// <param name="components">The components.</param>
    /// <param name="count">The number the space takes.</param>
    /// <returns>The first <see cref="SavedComponents"/> of them; zero past <paramref name="count"/>.</returns>
    private static Vector4 Save(ReadOnlySpan<float> components, int count)
    {
        Span<float> saved = stackalloc float[SavedComponents];
        saved.Clear();
        components[..Math.Min(Math.Min(count, components.Length), SavedComponents)].CopyTo(saved);
        return new(saved);
    }

    /// <summary>Converts one channel to a byte.</summary>
    /// <param name="value">The channel from 0 to 1.</param>
    /// <returns>The byte value.</returns>
    private static uint Channel(float value) => (uint)MathF.Round(Math.Clamp(value, 0, 1) * ChannelMax);
}
