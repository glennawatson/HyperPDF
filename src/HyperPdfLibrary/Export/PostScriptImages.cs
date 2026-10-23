// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Export;

/// <summary>Writes the images of a page as PostScript level 3 images: full colour images over white, and stencil masks as one-bit masks.</summary>
internal static class PostScriptImages
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The pixels in a byte of a mask.</summary>
    private const int ByteBits = 8;

    /// <summary>The coverage from which a mask pixel is painted.</summary>
    private const byte CoverageThreshold = 128;

    /// <summary>The most a channel can be.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The offset of the green channel in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha channel in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The bit that marks the first pixel of a mask byte.</summary>
    private const int TopBit = 0x80;

    /// <summary>The mask of the pixel position within a byte.</summary>
    private const int BitIndexMask = ByteBits - 1;

    /// <summary>Writes an image drawn into the unit square of user space.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="image">The image.</param>
    /// <param name="isMask">Whether the image is a stencil mask painted with the fill colour.</param>
    /// <param name="smooth">Whether the image asks for smoothing when scaled up.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="false"/> when the pixels could not be read and nothing was written.</returns>
    internal static bool TryWrite(PostScriptWriter writer, IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return false;
        }

        return isMask ? WriteMask(writer, image, ref state) : WriteColour(writer, image, smooth, ref state);
    }

    /// <summary>Writes a stencil mask.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="image">The image.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="false"/> when the coverage could not be read.</returns>
    private static bool WriteMask(PostScriptWriter writer, IPdfRenderImage image, ref GraphicsState state)
    {
        var width = image.Width;
        var height = image.Height;
        var coverage = new byte[width * height];
        if (!image.CopyPixels(new(width, height, PdfImagePixelFormat.Gray8), coverage, width))
        {
            return false;
        }

        var rowBytes = (width + ByteBits - 1) / ByteBits;
        var bits = new byte[rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (coverage[(y * width) + x] >= CoverageThreshold)
                {
                    bits[(y * rowBytes) + (x / ByteBits)] |= (byte)(TopBit >> (x & BitIndexMask));
                }
            }
        }

        writer.Word("gsave");
        writer.Concat(state.Ctm);
        writer.Color(state.Fill.Red, state.Fill.Green, state.Fill.Blue, state.FillAlpha);
        writer.Number(width);
        writer.Number(height);
        writer.Word("true");
        writer.Matrix(Upright(width, height));
        writer.Word("currentfile /ASCII85Decode filter /FlateDecode filter imagemask");
        writer.EndLine();
        writer.CompressedData(bits);
        writer.Line("grestore");
        return true;
    }

    /// <summary>Writes a colour image, placed over white because PostScript images have no alpha.</summary>
    /// <param name="writer">The PostScript writer.</param>
    /// <param name="image">The image.</param>
    /// <param name="smooth">Whether the image asks for smoothing.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="false"/> when the pixels could not be read.</returns>
    private static bool WriteColour(PostScriptWriter writer, IPdfRenderImage image, bool smooth, ref GraphicsState state)
    {
        var width = image.Width;
        var height = image.Height;
        var bgra = new byte[width * height * BgraBytes];
        if (!image.CopyPixels(new(width, height, PdfImagePixelFormat.Bgra8888), bgra, width * BgraBytes))
        {
            return false;
        }

        var rgb = PlaceOverWhite(bgra, Math.Clamp(state.FillAlpha, 0, 1));
        writer.Word("gsave");
        writer.Concat(state.Ctm);
        writer.Word("/DeviceRGB setcolorspace << /ImageType 1 /Width");
        writer.Number(width);
        writer.Word("/Height");
        writer.Number(height);
        writer.Word("/BitsPerComponent 8 /Decode [0 1 0 1 0 1] /ImageMatrix");
        writer.Matrix(Upright(width, height));
        writer.Word(smooth ? "/Interpolate true" : "/Interpolate false");
        writer.Word("/DataSource currentfile /ASCII85Decode filter /FlateDecode filter >> image");
        writer.EndLine();
        writer.CompressedData(rgb);
        writer.Line("grestore");
        return true;
    }

    /// <summary>Composites premultiplied pixels over white, with an extra opacity.</summary>
    /// <param name="bgra">The premultiplied pixels.</param>
    /// <param name="opacity">The opacity from 0 to 1 applied to the whole image.</param>
    /// <returns>Red, green and blue samples.</returns>
    private static byte[] PlaceOverWhite(byte[] bgra, float opacity)
    {
        var rgb = new byte[bgra.Length / BgraBytes * RgbBytes];
        for (int source = 0, target = 0; source < bgra.Length; source += BgraBytes, target += RgbBytes)
        {
            var cover = bgra[source + AlphaOffset] / ChannelMax * opacity;
            rgb[target] = Blend(bgra[source + RedOffset], opacity, cover);
            rgb[target + GreenOffset] = Blend(bgra[source + GreenOffset], opacity, cover);
            rgb[target + RedOffset] = Blend(bgra[source], opacity, cover);
        }

        return rgb;
    }

    /// <summary>Blends one premultiplied channel over white.</summary>
    /// <param name="premultiplied">The premultiplied channel.</param>
    /// <param name="opacity">The opacity applied to the whole image.</param>
    /// <param name="cover">How much of the pixel is covered: its alpha times the opacity.</param>
    /// <returns>The channel.</returns>
    private static byte Blend(byte premultiplied, float opacity, float cover) =>
        (byte)Math.Clamp((premultiplied * opacity) + (ChannelMax * (1 - cover)), 0, ChannelMax);

    /// <summary>Gets the matrix that maps the unit square of user space onto the image with its first row at the top.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The image matrix.</returns>
    private static Matrix3x2 Upright(int width, int height) => new(width, 0, 0, -height, 0, height);
}
