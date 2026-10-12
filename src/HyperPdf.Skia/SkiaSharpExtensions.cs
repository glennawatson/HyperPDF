// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace HyperPdf.Skia;

/// <summary>High-performance conversion extensions between Avalonia and SkiaSharp primitives.</summary>
internal static class SkiaSharpExtensions
{
    /// <summary>Extension members for <c>AlphaFormat</c>.</summary>
    /// <summary>Converts AlphaFormat values for Skia rendering.</summary>
    /// <param name="fmt">The value to convert.</param>
    extension(AlphaFormat fmt)
    {
        /// <summary>Converts an Avalonia <see cref = "AlphaFormat"/> to an <see cref = "SKAlphaType"/>.</summary>
        /// <returns>The Skia alpha type.</returns>
        /// <exception cref="ArgumentException">The alpha format is unknown.</exception>
        internal SKAlphaType ToSkAlphaType() => fmt switch
        {
            AlphaFormat.Opaque => SKAlphaType.Opaque,
            AlphaFormat.Premul => SKAlphaType.Premul,
            AlphaFormat.Unpremul => SKAlphaType.Unpremul,
            _ => throw new ArgumentException($"Unknown alpha format: {fmt}", nameof(fmt)),
        };
    }

    /// <summary>Extension members for <c>BitmapBlendingMode</c>.</summary>
    /// <summary>Converts BitmapBlendingMode values for Skia rendering.</summary>
    /// <param name="mode">The value to convert.</param>
    extension(BitmapBlendingMode mode)
    {
        /// <summary>Converts an Avalonia <see cref = "BitmapBlendingMode"/> to an <see cref = "SKBlendMode"/>.</summary>
        /// <returns>The Skia blend mode.</returns>
        internal SKBlendMode ToSKBlendMode() => mode switch
        {
            BitmapBlendingMode.Unspecified or BitmapBlendingMode.SourceOver => SKBlendMode.SrcOver,
            BitmapBlendingMode.Source => SKBlendMode.Src,
            BitmapBlendingMode.SourceIn => SKBlendMode.SrcIn,
            BitmapBlendingMode.SourceOut => SKBlendMode.SrcOut,
            BitmapBlendingMode.SourceAtop => SKBlendMode.SrcATop,
            BitmapBlendingMode.Destination => SKBlendMode.Dst,
            BitmapBlendingMode.DestinationIn => SKBlendMode.DstIn,
            BitmapBlendingMode.DestinationOut => SKBlendMode.DstOut,
            BitmapBlendingMode.DestinationOver => SKBlendMode.DstOver,
            BitmapBlendingMode.DestinationAtop => SKBlendMode.DstATop,
            BitmapBlendingMode.Xor => SKBlendMode.Xor,
            BitmapBlendingMode.Plus => SKBlendMode.Plus,
            BitmapBlendingMode.Screen => SKBlendMode.Screen,
            BitmapBlendingMode.Overlay => SKBlendMode.Overlay,
            BitmapBlendingMode.Darken => SKBlendMode.Darken,
            BitmapBlendingMode.Lighten => SKBlendMode.Lighten,
            BitmapBlendingMode.ColorDodge => SKBlendMode.ColorDodge,
            BitmapBlendingMode.ColorBurn => SKBlendMode.ColorBurn,
            BitmapBlendingMode.HardLight => SKBlendMode.HardLight,
            BitmapBlendingMode.SoftLight => SKBlendMode.SoftLight,
            BitmapBlendingMode.Difference => SKBlendMode.Difference,
            BitmapBlendingMode.Exclusion => SKBlendMode.Exclusion,
            BitmapBlendingMode.Multiply => SKBlendMode.Multiply,
            BitmapBlendingMode.Hue => SKBlendMode.Hue,
            BitmapBlendingMode.Saturation => SKBlendMode.Saturation,
            BitmapBlendingMode.Color => SKBlendMode.Color,
            BitmapBlendingMode.Luminosity => SKBlendMode.Luminosity,
            _ => SKBlendMode.SrcOver,
        };
    }

    /// <summary>Extension members for <c>BitmapInterpolationMode</c>.</summary>
    /// <summary>Converts BitmapInterpolationMode values for Skia rendering.</summary>
    /// <param name="mode">The value to convert.</param>
    extension(BitmapInterpolationMode mode)
    {
        /// <summary>Converts an Avalonia <see cref = "BitmapInterpolationMode"/> to <see cref = "SKSamplingOptions"/>.</summary>
        /// <param name = "isUpscaling">Whether the destination is larger than source.</param>
        /// <returns>The sampling options.</returns>
        internal SKSamplingOptions ToSKSamplingOptions(bool isUpscaling = true) => mode switch
        {
            BitmapInterpolationMode.None => new(SKFilterMode.Nearest, SKMipmapMode.None),
            BitmapInterpolationMode.Unspecified or BitmapInterpolationMode.LowQuality => new(SKFilterMode.Linear, SKMipmapMode.None),
            BitmapInterpolationMode.MediumQuality => new(SKFilterMode.Linear, SKMipmapMode.Linear),

            // Catmull-Rom keeps text edges crisp when enlarged; Mitchell's smoothing visibly softened page tiles.
            BitmapInterpolationMode.HighQuality => isUpscaling ? new(SKCubicResampler.CatmullRom) : new(SKFilterMode.Linear, SKMipmapMode.Linear),
            _ => new(SKFilterMode.Linear, SKMipmapMode.None),
        };
    }

    /// <summary>Extension members for <c>Color</c>.</summary>
    /// <summary>Converts Color values for Skia rendering.</summary>
    /// <param name="c">The value to convert.</param>
    extension(Color c)
    {
        /// <summary>Converts an Avalonia <see cref = "Color"/> to an <see cref = "SKColor"/>.</summary>
        /// <returns>The Skia color.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKColor ToSKColor() => new(c.R, c.G, c.B, c.A);
    }

    /// <summary>Extension members for <c>FontStyle</c>.</summary>
    /// <summary>Converts FontStyle values for Skia rendering.</summary>
    /// <param name="style">The value to convert.</param>
    extension(FontStyle style)
    {
        /// <summary>Converts an Avalonia <see cref = "FontStyle"/> to an <see cref = "SKFontStyleSlant"/>.</summary>
        /// <returns>The Skia font style slant.</returns>
        internal SKFontStyleSlant ToSkia() => style switch
        {
            FontStyle.Italic => SKFontStyleSlant.Italic,
            FontStyle.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright,
        };
    }

    /// <summary>Extension members for <c>GradientSpreadMethod</c>.</summary>
    /// <summary>Converts GradientSpreadMethod values for Skia rendering.</summary>
    /// <param name="spread">The value to convert.</param>
    extension(GradientSpreadMethod spread)
    {
        /// <summary>Converts an Avalonia <see cref = "GradientSpreadMethod"/> to an <see cref = "SKShaderTileMode"/>.</summary>
        /// <returns>The Skia shader tile mode.</returns>
        internal SKShaderTileMode ToSKShaderTileMode() => spread switch
        {
            GradientSpreadMethod.Pad => SKShaderTileMode.Clamp,
            GradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            GradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp,
        };
    }

    /// <summary>Extension members for <c>Matrix</c>.</summary>
    /// <summary>Converts Matrix values for Skia rendering.</summary>
    /// <param name="m">The value to convert.</param>
    extension(Matrix m)
    {
        /// <summary>Converts an Avalonia <see cref = "Matrix"/> to an <see cref = "SKMatrix"/>.</summary>
        /// <returns>The Skia 3x3 matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKMatrix ToSKMatrix() => new()
        {
            ScaleX = (float)m.M11,
            SkewX = (float)m.M21,
            TransX = (float)m.M31,
            SkewY = (float)m.M12,
            ScaleY = (float)m.M22,
            TransY = (float)m.M32,
            Persp0 = (float)m.M13,
            Persp1 = (float)m.M23,
            Persp2 = (float)m.M33,
        };

        /// <summary>Converts an Avalonia <see cref = "Matrix"/> to an <see cref = "SKMatrix44"/>.</summary>
        /// <returns>The Skia 4x4 matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKMatrix44 ToSKMatrix44() => new()
        {
            M00 = (float)m.M11,
            M01 = (float)m.M12,
            M02 = 0F,
            M03 = (float)m.M13,
            M10 = (float)m.M21,
            M11 = (float)m.M22,
            M12 = 0F,
            M13 = (float)m.M23,
            M20 = 0F,
            M21 = 0F,
            M22 = 1F,
            M23 = 0F,
            M30 = (float)m.M31,
            M31 = (float)m.M32,
            M32 = 0F,
            M33 = (float)m.M33,
        };
    }

    /// <summary>Extension members for <c>PenLineCap</c>.</summary>
    /// <summary>Converts PenLineCap values for Skia rendering.</summary>
    /// <param name="cap">The value to convert.</param>
    extension(PenLineCap cap)
    {
        /// <summary>Converts an Avalonia <see cref = "PenLineCap"/> to an <see cref = "SKStrokeCap"/>.</summary>
        /// <returns>The Skia stroke cap.</returns>
        internal SKStrokeCap ToSKStrokeCap() => cap switch
        {
            PenLineCap.Round => SKStrokeCap.Round,
            PenLineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt,
        };
    }

    /// <summary>Extension members for <c>PenLineJoin</c>.</summary>
    /// <summary>Converts PenLineJoin values for Skia rendering.</summary>
    /// <param name="join">The value to convert.</param>
    extension(PenLineJoin join)
    {
        /// <summary>Converts an Avalonia <see cref = "PenLineJoin"/> to an <see cref = "SKStrokeJoin"/>.</summary>
        /// <returns>The Skia stroke join.</returns>
        internal SKStrokeJoin ToSKStrokeJoin() => join switch
        {
            PenLineJoin.Bevel => SKStrokeJoin.Bevel,
            PenLineJoin.Round => SKStrokeJoin.Round,
            _ => SKStrokeJoin.Miter,
        };
    }

    /// <summary>Extension members for <c>PixelFormat</c>.</summary>
    /// <summary>Converts PixelFormat values for Skia rendering.</summary>
    /// <param name="fmt">The value to convert.</param>
    extension(PixelFormat fmt)
    {
        /// <summary>Converts an Avalonia <see cref = "PixelFormat"/> to an <see cref = "SKColorType"/>.</summary>
        /// <returns>The Skia color type.</returns>
        /// <exception cref="ArgumentException">The pixel format is unsupported.</exception>
        internal SKColorType ToSkColorType()
        {
            if (fmt == PixelFormat.Rgb565)
            {
                return SKColorType.Rgb565;
            }

            if (fmt == PixelFormat.Bgra8888)
            {
                return SKColorType.Bgra8888;
            }

            if (fmt == PixelFormat.Rgba8888)
            {
                return SKColorType.Rgba8888;
            }

            if (fmt == PixelFormat.Rgb32)
            {
                return SKColorType.Rgb888x;
            }

            throw new ArgumentException($"Unsupported pixel format: {fmt}", nameof(fmt));
        }
    }

    /// <summary>Extension members for <c>PixelRect</c>.</summary>
    /// <summary>Converts PixelRect values for Skia rendering.</summary>
    /// <param name="r">The value to convert.</param>
    extension(PixelRect r)
    {
        /// <summary>Converts an Avalonia <see cref = "PixelRect"/> to an <see cref = "SKRectI"/>.</summary>
        /// <returns>The Skia integer rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKRectI ToSKRectI() => new(r.X, r.Y, r.Right, r.Bottom);
    }

    /// <summary>Converts Point values for Skia rendering.</summary>
    /// <param name="p">The value to convert.</param>
    extension(Point p)
    {
        /// <summary>Converts an Avalonia <see cref = "Point"/> to a <see cref = "SKPoint"/>.</summary>
        /// <returns>The Skia point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKPoint ToSKPoint() => new((float)p.X, (float)p.Y);
    }

    /// <summary>Extension members for <c>Rect</c>.</summary>
    /// <summary>Converts Rect values for Skia rendering.</summary>
    /// <param name="r">The value to convert.</param>
    extension(Rect r)
    {
        /// <summary>Converts an Avalonia <see cref = "Rect"/> to an <see cref = "SKRect"/>.</summary>
        /// <returns>The Skia rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKRect ToSKRect() => new((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    }

    /// <summary>Extension members for <c>SKAlphaType</c>.</summary>
    /// <summary>Converts SKAlphaType values for Skia rendering.</summary>
    /// <param name="alphaType">The value to convert.</param>
    extension(SKAlphaType alphaType)
    {
        /// <summary>Converts an <see cref = "SKAlphaType"/> to an Avalonia <see cref = "AlphaFormat"/>.</summary>
        /// <returns>The Avalonia alpha format.</returns>
        internal AlphaFormat ToAlphaFormat() => alphaType switch
        {
            SKAlphaType.Opaque => AlphaFormat.Opaque,
            SKAlphaType.Premul => AlphaFormat.Premul,
            SKAlphaType.Unpremul => AlphaFormat.Unpremul,
            _ => AlphaFormat.Premul,
        };
    }

    /// <summary>Extension members for <c>SKColorType</c>.</summary>
    /// <summary>Converts SKColorType values for Skia rendering.</summary>
    /// <param name="colorType">The value to convert.</param>
    extension(SKColorType colorType)
    {
        /// <summary>Converts an <see cref = "SKColorType"/> to an Avalonia <see cref = "PixelFormat"/>.</summary>
        /// <returns>The Avalonia pixel format, or null if unmappable.</returns>
        internal PixelFormat? ToAvalonia() => colorType switch
        {
            SKColorType.Rgb565 => PixelFormat.Rgb565,
            SKColorType.Bgra8888 => PixelFormat.Bgra8888,
            SKColorType.Rgba8888 => PixelFormat.Rgba8888,
            SKColorType.Rgb888x => PixelFormat.Rgb32,
            _ => null,
        };
    }

    /// <summary>Extension members for <c>SKFontStyleSlant</c>.</summary>
    /// <summary>Converts SKFontStyleSlant values for Skia rendering.</summary>
    /// <param name="slant">The value to convert.</param>
    extension(SKFontStyleSlant slant)
    {
        /// <summary>Converts an <see cref = "SKFontStyleSlant"/> to an Avalonia <see cref = "FontStyle"/>.</summary>
        /// <returns>The Avalonia font style.</returns>
        internal FontStyle ToAvalonia() => slant switch
        {
            SKFontStyleSlant.Italic => FontStyle.Italic,
            SKFontStyleSlant.Oblique => FontStyle.Oblique,
            _ => FontStyle.Normal,
        };
    }

    /// <summary>Extension members for <c>SKMatrix44</c>.</summary>
    /// <summary>Converts SKMatrix44 values for Skia rendering.</summary>
    /// <param name="m">The value to convert.</param>
    extension(SKMatrix44 m)
    {
        /// <summary>Converts an <see cref = "SKMatrix44"/> to an Avalonia <see cref = "Matrix"/>.</summary>
        /// <returns>The Avalonia 3x3 matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Matrix ToAvaloniaMatrix() => new(m.M00, m.M01, m.M03, m.M10, m.M11, m.M13, m.M30, m.M31, m.M33);
    }

    /// <summary>Extension members for <c>SKRect</c>.</summary>
    /// <summary>Converts SKRect values for Skia rendering.</summary>
    /// <param name="r">The value to convert.</param>
    extension(SKRect r)
    {
        /// <summary>Converts an <see cref = "SKRect"/> to an Avalonia <see cref = "Rect"/>.</summary>
        /// <returns>The Avalonia rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Rect ToAvaloniaRect() => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Extension members for <c>SKRectI</c>.</summary>
    /// <summary>Converts SKRectI values for Skia rendering.</summary>
    /// <param name="r">The value to convert.</param>
    extension(SKRectI r)
    {
        /// <summary>Converts an <see cref = "SKRectI"/> to an Avalonia <see cref = "PixelRect"/>.</summary>
        /// <returns>The Avalonia pixel rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PixelRect ToAvaloniaPixelRect() => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        /// <summary>Converts an <see cref = "SKRectI"/> to an Avalonia <see cref = "LtrbPixelRect"/>.</summary>
        /// <returns>The Avalonia Ltrb pixel rect.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LtrbPixelRect ToAvaloniaLtrbPixelRect() => new() { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
    }

    /// <summary>Extension members for <c>Vector</c>.</summary>
    /// <summary>Converts Vector values for Skia rendering.</summary>
    /// <param name="v">The value to convert.</param>
    extension(Vector v)
    {
        /// <summary>Converts an Avalonia <see cref = "Vector"/> to a <see cref = "SKPoint"/>.</summary>
        /// <returns>The Skia point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SKPoint ToSKPoint() => new((float)v.X, (float)v.Y);
    }
}
