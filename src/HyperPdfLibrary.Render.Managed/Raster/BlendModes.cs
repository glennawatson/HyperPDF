// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// The blend modes of PDF transparency (ISO 32000-2, 11.3.5). Each mixes a source colour with the colour already there, where both
/// are present; where only one is, that one shows unchanged.
/// </summary>
internal static class BlendModes
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>Half, where several modes change their formula.</summary>
    private const float Half = 0.5F;

    /// <summary>A quarter, where soft light changes its formula.</summary>
    private const float Quarter = 0.25F;

    /// <summary>The first factor of soft light's curve for dark backdrops.</summary>
    private const float SoftA = 16F;

    /// <summary>The second factor of soft light's curve for dark backdrops.</summary>
    private const float SoftB = 12F;

    /// <summary>The third factor of soft light's curve for dark backdrops.</summary>
    private const float SoftC = 4F;

    /// <summary>How much red adds to the brightness of a colour.</summary>
    private const float RedBrightness = 0.3F;

    /// <summary>How much green adds to the brightness of a colour.</summary>
    private const float GreenBrightness = 0.59F;

    /// <summary>How much blue adds to the brightness of a colour.</summary>
    private const float BlueBrightness = 0.11F;

    /// <summary>The shift of the alpha byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte of a pixel.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>Gets how much each of red, green and blue adds to the brightness of a colour.</summary>
    private static Vector3 Brightness => new(RedBrightness, GreenBrightness, BlueBrightness);

    /// <summary>Blends a line of colours onto a line with a blend mode.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel of the run.</param>
    /// <param name="source">The premultiplied colour of each pixel of the run.</param>
    internal static void Apply(PdfBlendMode mode, Span<uint> pixels, ReadOnlySpan<byte> coverage, ReadOnlySpan<uint> source)
    {
        for (var i = 0; i < coverage.Length; i++)
        {
            var value = coverage[i];
            if (value == 0)
            {
                continue;
            }

            var top = value == byte.MaxValue ? source[i] : SpanBlender.Scale(source[i], value);
            var under = pixels[i];

            // Two opaque pixels need none of the alpha arithmetic: dividing by an alpha of one changes nothing, and the blend is all that shows.
            pixels[i] = (top >> AlphaShift) == byte.MaxValue && (under >> AlphaShift) == byte.MaxValue ? BlendOpaque(mode, top, under) : Blend(mode, top, under);
        }
    }

    /// <summary>Blends one opaque pixel onto another, which gives the bytes <see cref="Blend"/> gives for two opaque pixels.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="source">The pixel on top.</param>
    /// <param name="backdrop">The pixel under it.</param>
    /// <returns>The blended pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint BlendOpaque(PdfBlendMode mode, uint source, uint backdrop) =>
        ToPixel(Vector3.Clamp(Mix(mode, ToColor(backdrop), ToColor(source)), Vector3.Zero, Vector3.One), 1F);

    /// <summary>Blends one premultiplied pixel onto another.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="source">The pixel on top.</param>
    /// <param name="backdrop">The pixel under it.</param>
    /// <returns>The blended pixel.</returns>
    internal static uint Blend(PdfBlendMode mode, uint source, uint backdrop)
    {
        var sourceAlpha = (source >> AlphaShift) / Full;
        var backdropAlpha = (backdrop >> AlphaShift) / Full;
        if (sourceAlpha <= 0)
        {
            return backdrop;
        }

        if (backdropAlpha <= 0)
        {
            // With nothing under it there is nothing to blend with.
            return source;
        }

        var top = ToColor(source) / sourceAlpha;
        var under = ToColor(backdrop) / backdropAlpha;
        var mixed = Vector3.Clamp(Mix(mode, under, top), Vector3.Zero, Vector3.One);

        // Where only one of the two is present it shows as it is, and where both are the blend shows.
        var color = ((1F - sourceAlpha) * backdropAlpha * under) + ((1F - backdropAlpha) * sourceAlpha * top) + (sourceAlpha * backdropAlpha * mixed);
        return ToPixel(color, sourceAlpha + backdropAlpha - (sourceAlpha * backdropAlpha));
    }

    /// <summary>Mixes two colours with a blend mode.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="under">The backdrop colour, not premultiplied.</param>
    /// <param name="top">The source colour, not premultiplied.</param>
    /// <returns>The mixed colour.</returns>
    private static Vector3 Mix(PdfBlendMode mode, Vector3 under, Vector3 top) => mode switch
    {
        PdfBlendMode.Multiply => under * top,
        PdfBlendMode.Screen => under + top - (under * top),
        PdfBlendMode.Darken => Vector3.Min(under, top),
        PdfBlendMode.Lighten => Vector3.Max(under, top),
        PdfBlendMode.Difference => Vector3.Abs(under - top),
        PdfBlendMode.Exclusion => under + top - ((under * top) + (under * top)),
        >= PdfBlendMode.Hue => MixWhole(mode, under, top),
        _ => new(MixChannel(mode, under.X, top.X), MixChannel(mode, under.Y, top.Y), MixChannel(mode, under.Z, top.Z)),
    };

    /// <summary>Mixes one channel with a blend mode whose formula has cases.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float MixChannel(PdfBlendMode mode, float under, float top) => mode switch
    {
        PdfBlendMode.Overlay => Overlay(under, top),
        PdfBlendMode.HardLight => HardLight(under, top),
        PdfBlendMode.ColorDodge => Dodge(under, top),
        PdfBlendMode.ColorBurn => Burn(under, top),
        PdfBlendMode.SoftLight => SoftLight(under, top),
        _ => top,
    };

    /// <summary>Multiplies or screens a channel, by how light the backdrop is.</summary>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float Overlay(float under, float top) =>
        under <= Half ? top * (under + under) : top + (under + under - 1F) - (top * (under + under - 1F));

    /// <summary>Multiplies or screens a channel, by how light the source is.</summary>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float HardLight(float under, float top) =>
        top <= Half ? under * (top + top) : under + (top + top - 1F) - (under * (top + top - 1F));

    /// <summary>Lightens the backdrop to reflect the source.</summary>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float Dodge(float under, float top)
    {
        if (under <= 0)
        {
            return 0;
        }

        return top >= 1F ? 1F : MathF.Min(1F, under / (1F - top));
    }

    /// <summary>Darkens the backdrop to reflect the source.</summary>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float Burn(float under, float top)
    {
        if (under >= 1F)
        {
            return 1F;
        }

        return top <= 0 ? 0 : 1F - MathF.Min(1F, (1F - under) / top);
    }

    /// <summary>Darkens or lightens a channel softly, by how light the source is.</summary>
    /// <param name="under">The backdrop channel.</param>
    /// <param name="top">The source channel.</param>
    /// <returns>The mixed channel.</returns>
    private static float SoftLight(float under, float top)
    {
        if (top <= Half)
        {
            return under - ((1F - top - top) * under * (1F - under));
        }

        var curve = under <= Quarter ? ((((SoftA * under) - SoftB) * under) + SoftC) * under : MathF.Sqrt(under);
        return under + ((top + top - 1F) * (curve - under));
    }

    /// <summary>Mixes two colours with a mode that works on hue, saturation and brightness rather than on each channel.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <param name="under">The backdrop colour.</param>
    /// <param name="top">The source colour.</param>
    /// <returns>The mixed colour.</returns>
    private static Vector3 MixWhole(PdfBlendMode mode, Vector3 under, Vector3 top) => mode switch
    {
        PdfBlendMode.Hue => WithBrightness(WithSaturation(top, Saturation(under)), Vector3.Dot(under, Brightness)),
        PdfBlendMode.Saturation => WithBrightness(WithSaturation(under, Saturation(top)), Vector3.Dot(under, Brightness)),
        PdfBlendMode.Color => WithBrightness(top, Vector3.Dot(under, Brightness)),
        _ => WithBrightness(under, Vector3.Dot(top, Brightness)),
    };

    /// <summary>Gets how far apart the largest and smallest channel of a colour are.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The saturation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Saturation(Vector3 color) => MathF.Max(color.X, MathF.Max(color.Y, color.Z)) - MathF.Min(color.X, MathF.Min(color.Y, color.Z));

    /// <summary>Gives a colour another saturation, keeping the order of its channels.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="saturation">The saturation wanted.</param>
    /// <returns>The colour, with its smallest channel at zero.</returns>
    private static Vector3 WithSaturation(Vector3 color, float saturation)
    {
        var smallest = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        var range = Saturation(color);
        return range > 0 ? (color - new Vector3(smallest)) * (saturation / range) : Vector3.Zero;
    }

    /// <summary>Gives a colour another brightness, pulling it back towards gray where a channel would leave the range.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="brightness">The brightness wanted.</param>
    /// <returns>The colour at that brightness, with every channel from 0 to 1.</returns>
    private static Vector3 WithBrightness(Vector3 color, float brightness)
    {
        var moved = color + new Vector3(brightness - Vector3.Dot(color, Brightness));
        var gray = Vector3.Dot(moved, Brightness);
        var smallest = MathF.Min(moved.X, MathF.Min(moved.Y, moved.Z));
        var largest = MathF.Max(moved.X, MathF.Max(moved.Y, moved.Z));
        if (smallest < 0)
        {
            moved = new Vector3(gray) + ((moved - new Vector3(gray)) * (gray / (gray - smallest)));
        }

        return largest > 1F ? new Vector3(gray) + ((moved - new Vector3(gray)) * ((1F - gray) / (largest - gray))) : moved;
    }

    /// <summary>Reads the colour bytes of a pixel as red, green and blue from 0 to 1.</summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns>The colour, still premultiplied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ToColor(uint pixel) => new Vector3((pixel >> RedShift) & ByteMask, (pixel >> GreenShift) & ByteMask, pixel & ByteMask) / Full;

    /// <summary>Packs a premultiplied colour and its alpha into a pixel.</summary>
    /// <param name="color">The premultiplied colour, 0 to 1.</param>
    /// <param name="alpha">The alpha, 0 to 1.</param>
    /// <returns>The pixel.</returns>
    private static uint ToPixel(Vector3 color, float alpha)
    {
        var bytes = Vector3.Clamp(color, Vector3.Zero, new(alpha)) * Full;
        return (uint)(bytes.Z + Half) | ((uint)(bytes.Y + Half) << GreenShift) | ((uint)(bytes.X + Half) << RedShift) | ((uint)((alpha * Full) + Half) << AlphaShift);
    }
}
