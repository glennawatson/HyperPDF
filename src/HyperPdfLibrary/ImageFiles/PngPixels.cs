// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Turns the samples read from a PNG file into the raster formats a PDF holds.</summary>
internal static class PngPixels
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes of a grey transparency key.</summary>
    private const int GreyKeyBytes = 2;

    /// <summary>The bytes of a colour transparency key.</summary>
    private const int ColourKeyBytes = 6;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The widest sample.</summary>
    private const int WidestSample = 16;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenIndex = 1;

    /// <summary>The offset of the blue sample in a pixel.</summary>
    private const int BlueIndex = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The bytes of the green key.</summary>
    private const int GreenKey = 2;

    /// <summary>The bytes of the blue key.</summary>
    private const int BlueKey = 4;

    /// <summary>Converts samples to a raster image.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples, row by row.</param>
    /// <param name="palette">The palette, or empty.</param>
    /// <param name="transparency">The transparency chunk's data, or empty.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">A palette picture has no palette, or an index is outside it.</exception>
    internal static PdfRasterImage Convert(PngHeader header, ushort[] samples, byte[] palette, byte[] transparency) => header.ColorType switch
    {
        PngHeader.Grey => Grey(header, samples, transparency),
        PngHeader.Colour => Colour(header, samples, transparency),
        PngHeader.Palette => Indexed(header, samples, palette, transparency),
        PngHeader.GreyAlpha => GreyWithAlpha(header, samples),
        _ => ColourWithAlpha(header, samples),
    };

    /// <summary>Scales a sample to eight bits.</summary>
    /// <param name="value">The sample.</param>
    /// <param name="depth">The bits in the sample.</param>
    /// <returns>The sample from 0 to 255.</returns>
    private static byte Scale(ushort value, int depth) => depth switch
    {
        ByteBits => (byte)value,
        WidestSample => (byte)(value >> ByteBits),
        _ => (byte)(value * byte.MaxValue / ((1 << depth) - 1)),
    };

    /// <summary>Makes a grey picture: one bit, one byte, or with a transparent grey, BGRA.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="transparency">The grey that is transparent, or empty.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Grey(PngHeader header, ushort[] samples, byte[] transparency)
    {
        if (transparency.Length >= GreyKeyBytes)
        {
            var key = (ushort)((transparency[0] << ByteBits) | transparency[1]);
            var bgra = new byte[samples.Length * BgraBytes];
            for (var i = 0; i < samples.Length; i++)
            {
                var grey = Scale(samples[i], header.Depth);
                bgra[i * BgraBytes] = grey;
                bgra[(i * BgraBytes) + GreenIndex] = grey;
                bgra[(i * BgraBytes) + BlueIndex] = grey;
                bgra[(i * BgraBytes) + AlphaIndex] = samples[i] == key ? (byte)0 : byte.MaxValue;
            }

            return new(PdfRasterFormat.Bgra32, header.Width, header.Height, bgra);
        }

        return header.Depth == 1 ? Bilevel(header, samples) : Gray8(header, samples);
    }

    /// <summary>Packs one-bit grey samples, where a set bit is white.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples, each 0 or 1.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Bilevel(PngHeader header, ushort[] samples)
    {
        var rowBytes = (header.Width + ByteBits - 1) / ByteBits;
        var packed = new byte[rowBytes * header.Height];
        for (var y = 0; y < header.Height; y++)
        {
            for (var x = 0; x < header.Width; x++)
            {
                if (samples[(y * header.Width) + x] != 0)
                {
                    packed[(y * rowBytes) + (x / ByteBits)] |= (byte)(0x80 >> (x % ByteBits));
                }
            }
        }

        return new(PdfRasterFormat.Bilevel1, header.Width, header.Height, packed);
    }

    /// <summary>Makes an eight-bit grey picture.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Gray8(PngHeader header, ushort[] samples)
    {
        var grey = new byte[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            grey[i] = Scale(samples[i], header.Depth);
        }

        return new(PdfRasterFormat.Gray8, header.Width, header.Height, grey);
    }

    /// <summary>Makes a colour picture: RGB, or with a transparent colour, BGRA.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples, three to a pixel.</param>
    /// <param name="transparency">The colour that is transparent, or empty.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Colour(PngHeader header, ushort[] samples, byte[] transparency)
    {
        var pixels = samples.Length / RgbBytes;
        if (transparency.Length < ColourKeyBytes)
        {
            var rgb = new byte[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                rgb[i] = Scale(samples[i], header.Depth);
            }

            return new(PdfRasterFormat.Rgb24, header.Width, header.Height, rgb);
        }

        var bgra = new byte[pixels * BgraBytes];
        for (var i = 0; i < pixels; i++)
        {
            var red = samples[i * RgbBytes];
            var green = samples[(i * RgbBytes) + GreenIndex];
            var blue = samples[(i * RgbBytes) + BlueIndex];
            var clear = red == ReadKey(transparency, 0) && green == ReadKey(transparency, GreenKey) && blue == ReadKey(transparency, BlueKey);
            bgra[i * BgraBytes] = Scale(blue, header.Depth);
            bgra[(i * BgraBytes) + GreenIndex] = Scale(green, header.Depth);
            bgra[(i * BgraBytes) + BlueIndex] = Scale(red, header.Depth);
            bgra[(i * BgraBytes) + AlphaIndex] = clear ? (byte)0 : byte.MaxValue;
        }

        return new(PdfRasterFormat.Bgra32, header.Width, header.Height, bgra);
    }

    /// <summary>Reads a 16-bit transparency key.</summary>
    /// <param name="transparency">The chunk's data.</param>
    /// <param name="offset">The key's offset.</param>
    /// <returns>The key.</returns>
    private static ushort ReadKey(byte[] transparency, int offset) => (ushort)((transparency[offset] << ByteBits) | transparency[offset + 1]);

    /// <summary>Makes a picture from a palette: RGB, or with transparent entries, BGRA.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The palette indexes.</param>
    /// <param name="palette">The palette.</param>
    /// <param name="transparency">The alpha of each entry, or empty.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">There is no palette, or an index is outside it.</exception>
    private static PdfRasterImage Indexed(PngHeader header, ushort[] samples, byte[] palette, byte[] transparency)
    {
        if (palette.Length < RgbBytes)
        {
            throw new InvalidDataException("The PNG needs a palette and has none.");
        }

        var entries = palette.Length / RgbBytes;
        var hasAlpha = transparency.Length > 0;
        var step = hasAlpha ? BgraBytes : RgbBytes;
        var pixels = new byte[samples.Length * step];
        for (var i = 0; i < samples.Length; i++)
        {
            var index = samples[i];
            if (index >= entries)
            {
                throw new InvalidDataException("A PNG pixel names a palette entry that does not exist.");
            }

            if (hasAlpha)
            {
                pixels[i * step] = palette[(index * RgbBytes) + BlueIndex];
                pixels[(i * step) + GreenIndex] = palette[(index * RgbBytes) + GreenIndex];
                pixels[(i * step) + BlueIndex] = palette[index * RgbBytes];
                pixels[(i * step) + AlphaIndex] = index < transparency.Length ? transparency[index] : byte.MaxValue;
            }
            else
            {
                pixels[i * step] = palette[index * RgbBytes];
                pixels[(i * step) + GreenIndex] = palette[(index * RgbBytes) + GreenIndex];
                pixels[(i * step) + BlueIndex] = palette[(index * RgbBytes) + BlueIndex];
            }
        }

        return new(hasAlpha ? PdfRasterFormat.Bgra32 : PdfRasterFormat.Rgb24, header.Width, header.Height, pixels);
    }

    /// <summary>Makes a BGRA picture from grey and alpha samples.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples, a grey and an alpha for each pixel.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage GreyWithAlpha(PngHeader header, ushort[] samples)
    {
        var pixels = samples.Length / GreyKeyBytes;
        var bgra = new byte[pixels * BgraBytes];
        for (var i = 0; i < pixels; i++)
        {
            var grey = Scale(samples[i * GreyKeyBytes], header.Depth);
            bgra[i * BgraBytes] = grey;
            bgra[(i * BgraBytes) + GreenIndex] = grey;
            bgra[(i * BgraBytes) + BlueIndex] = grey;
            bgra[(i * BgraBytes) + AlphaIndex] = Scale(samples[(i * GreyKeyBytes) + 1], header.Depth);
        }

        return new(PdfRasterFormat.Bgra32, header.Width, header.Height, bgra);
    }

    /// <summary>Makes a BGRA picture from red, green, blue and alpha samples.</summary>
    /// <param name="header">The header.</param>
    /// <param name="samples">The samples, four for each pixel.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage ColourWithAlpha(PngHeader header, ushort[] samples)
    {
        var bgra = new byte[samples.Length];
        for (var i = 0; i < samples.Length; i += BgraBytes)
        {
            bgra[i] = Scale(samples[i + BlueIndex], header.Depth);
            bgra[i + GreenIndex] = Scale(samples[i + GreenIndex], header.Depth);
            bgra[i + BlueIndex] = Scale(samples[i], header.Depth);
            bgra[i + AlphaIndex] = Scale(samples[i + AlphaIndex], header.Depth);
        }

        return new(PdfRasterFormat.Bgra32, header.Width, header.Height, bgra);
    }
}
