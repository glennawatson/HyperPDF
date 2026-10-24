// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Turns the samples of a TIFF image into the raster formats a PDF holds.</summary>
internal static class TiffPixels
{
    /// <summary>The photometric value where zero is white.</summary>
    private const int WhiteIsZero = 0;

    /// <summary>The photometric value where zero is black.</summary>
    private const int BlackIsZero = 1;

    /// <summary>The photometric value for red, green and blue.</summary>
    private const int Rgb = 2;

    /// <summary>The photometric value for a palette.</summary>
    private const int Palette = 3;

    /// <summary>The photometric value for cyan, magenta, yellow and black.</summary>
    private const int Cmyk = 5;

    /// <summary>The meaning of an extra sample that is premultiplied alpha.</summary>
    private const int AssociatedAlpha = 1;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The widest sample.</summary>
    private const int WidestSample = 16;

    /// <summary>The samples of a CMYK pixel.</summary>
    private const int CmykSamples = 4;

    /// <summary>The samples of a grey pixel with alpha.</summary>
    private const int GreyAlphaSamples = 2;

    /// <summary>The offset of the second sample or channel.</summary>
    private const int SecondIndex = 1;

    /// <summary>The offset of the third sample or channel.</summary>
    private const int ThirdIndex = 2;

    /// <summary>The offset of the alpha channel in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The flag for the leftmost pixel of a packed byte.</summary>
    private const int TopBit = 0x80;

    /// <summary>Converts samples to a raster image.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples, pixel by pixel.</param>
    /// <param name="colorMap">The palette tag's values: all the reds, then all the greens, then all the blues; empty when there is none.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The photometric interpretation or sample count is not one this reader handles.</exception>
    internal static PdfRasterImage Convert(in TiffInfo info, ushort[] samples, uint[] colorMap) => info.Photometric switch
    {
        WhiteIsZero or BlackIsZero => Grey(info, samples),
        Rgb => Colour(info, samples),
        Palette => Indexed(info, samples, colorMap),
        Cmyk when info.SamplesPerPixel >= CmykSamples => Separated(info, samples),
        _ => throw new InvalidDataException("This reader does not handle the TIFF's colour model."),
    };

    /// <summary>Scales a sample to eight bits.</summary>
    /// <param name="value">The sample.</param>
    /// <param name="bits">The bits in the sample.</param>
    /// <returns>The sample from 0 to 255.</returns>
    private static byte Scale(ushort value, int bits) => bits switch
    {
        ByteBits => (byte)value,
        WidestSample => (byte)(value >> ByteBits),
        _ => (byte)(value * byte.MaxValue / ((1 << bits) - 1)),
    };

    /// <summary>Makes a grey picture: one bit, eight bits, or with alpha, BGRA.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Grey(in TiffInfo info, ushort[] samples)
    {
        var inverted = info.Photometric == WhiteIsZero;
        if (info.BitsPerSample == 1 && info.SamplesPerPixel == 1)
        {
            return Bilevel(info, samples, inverted);
        }

        var hasAlpha = info.Alpha != 0 && info.SamplesPerPixel >= GreyAlphaSamples;
        var pixels = info.Width * info.Height;
        var output = new byte[pixels * (hasAlpha ? BgraBytes : 1)];
        for (var i = 0; i < pixels; i++)
        {
            var grey = ReadGrey(info, samples, i, inverted);
            if (!hasAlpha)
            {
                output[i] = grey;
                continue;
            }

            var alpha = Scale(samples[(i * info.SamplesPerPixel) + SecondIndex], info.BitsPerSample);
            WriteBgra(output.AsSpan(i * BgraBytes, BgraBytes), new(grey, grey, grey, alpha), info.Alpha);
        }

        return new(hasAlpha ? PdfRasterFormat.Bgra32 : PdfRasterFormat.Gray8, info.Width, info.Height, output);
    }

    /// <summary>Reads the grey of a pixel as eight bits, with white at 255.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="pixel">The pixel's index.</param>
    /// <param name="inverted">Whether zero is white.</param>
    /// <returns>The grey.</returns>
    private static byte ReadGrey(in TiffInfo info, ushort[] samples, int pixel, bool inverted)
    {
        var value = samples[pixel * info.SamplesPerPixel];
        return Scale((ushort)(inverted ? (1 << info.BitsPerSample) - 1 - value : value), info.BitsPerSample);
    }

    /// <summary>Packs one-bit samples into a one-bit picture, where a set bit is white.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples, each 0 or 1.</param>
    /// <param name="zeroIsWhite">Whether a sample of 0 is white.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Bilevel(in TiffInfo info, ushort[] samples, bool zeroIsWhite)
    {
        var rowBytes = (info.Width + ByteBits - 1) / ByteBits;
        var packed = new byte[rowBytes * info.Height];
        for (var y = 0; y < info.Height; y++)
        {
            for (var x = 0; x < info.Width; x++)
            {
                if ((samples[(y * info.Width) + x] == 0) == zeroIsWhite)
                {
                    packed[(y * rowBytes) + (x / ByteBits)] |= (byte)(TopBit >> (x % ByteBits));
                }
            }
        }

        return new(PdfRasterFormat.Bilevel1, info.Width, info.Height, packed);
    }

    /// <summary>Makes a colour picture: RGB, or with alpha, BGRA.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The pixels have fewer than three samples.</exception>
    private static PdfRasterImage Colour(in TiffInfo info, ushort[] samples)
    {
        var step = info.SamplesPerPixel;
        if (step < RgbBytes)
        {
            throw new InvalidDataException("A TIFF colour picture needs three samples a pixel.");
        }

        var hasAlpha = info.Alpha != 0 && step > RgbBytes;
        var pixels = info.Width * info.Height;
        var output = new byte[pixels * (hasAlpha ? BgraBytes : RgbBytes)];
        for (var i = 0; i < pixels; i++)
        {
            var red = Scale(samples[i * step], info.BitsPerSample);
            var green = Scale(samples[(i * step) + SecondIndex], info.BitsPerSample);
            var blue = Scale(samples[(i * step) + ThirdIndex], info.BitsPerSample);
            if (hasAlpha)
            {
                WriteBgra(output.AsSpan(i * BgraBytes, BgraBytes), new(blue, green, red, Scale(samples[(i * step) + RgbBytes], info.BitsPerSample)), info.Alpha);
                continue;
            }

            output[i * RgbBytes] = red;
            output[(i * RgbBytes) + SecondIndex] = green;
            output[(i * RgbBytes) + ThirdIndex] = blue;
        }

        return new(hasAlpha ? PdfRasterFormat.Bgra32 : PdfRasterFormat.Rgb24, info.Width, info.Height, output);
    }

    /// <summary>Writes a BGRA pixel, undoing premultiplied alpha when the file has it.</summary>
    /// <param name="target">Receives the four bytes.</param>
    /// <param name="pixel">The blue, green, red and alpha of the pixel.</param>
    /// <param name="meaning">The meaning of the alpha: 1 for premultiplied colour.</param>
    private static void WriteBgra(Span<byte> target, in BgraPixel pixel, int meaning)
    {
        target[0] = Straight(pixel.Blue, pixel.Alpha, meaning);
        target[SecondIndex] = Straight(pixel.Green, pixel.Alpha, meaning);
        target[ThirdIndex] = Straight(pixel.Red, pixel.Alpha, meaning);
        target[AlphaIndex] = pixel.Alpha;
    }

    /// <summary>Makes a colour picture from palette indexes.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The indexes.</param>
    /// <param name="colorMap">All the reds, then all the greens, then all the blues, 16 bits each.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The palette is missing or too short.</exception>
    private static PdfRasterImage Indexed(in TiffInfo info, ushort[] samples, uint[] colorMap)
    {
        var entries = 1 << info.BitsPerSample;
        if (colorMap.Length < entries * RgbBytes)
        {
            throw new InvalidDataException("The TIFF's palette is missing or too short.");
        }

        var pixels = info.Width * info.Height;
        var output = new byte[pixels * RgbBytes];
        for (var i = 0; i < pixels; i++)
        {
            var index = samples[i * info.SamplesPerPixel];
            output[i * RgbBytes] = (byte)(colorMap[index] >> ByteBits);
            output[(i * RgbBytes) + SecondIndex] = (byte)(colorMap[entries + index] >> ByteBits);
            output[(i * RgbBytes) + ThirdIndex] = (byte)(colorMap[entries + entries + index] >> ByteBits);
        }

        return new(PdfRasterFormat.Rgb24, info.Width, info.Height, output);
    }

    /// <summary>Makes a colour picture from cyan, magenta, yellow and black samples, by the plain conversion.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage Separated(in TiffInfo info, ushort[] samples)
    {
        var pixels = info.Width * info.Height;
        var output = new byte[pixels * RgbBytes];
        for (var i = 0; i < pixels; i++)
        {
            var key = byte.MaxValue - Scale(samples[(i * info.SamplesPerPixel) + RgbBytes], info.BitsPerSample);
            for (var channel = 0; channel < RgbBytes; channel++)
            {
                var ink = Scale(samples[(i * info.SamplesPerPixel) + channel], info.BitsPerSample);
                output[(i * RgbBytes) + channel] = (byte)((byte.MaxValue - ink) * key / byte.MaxValue);
            }
        }

        return new(PdfRasterFormat.Rgb24, info.Width, info.Height, output);
    }

    /// <summary>Turns a premultiplied colour channel into a straight one.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="meaning">The meaning of the alpha: 1 for premultiplied colour.</param>
    /// <returns>The straight channel.</returns>
    private static byte Straight(byte channel, byte alpha, int meaning) =>
        meaning != AssociatedAlpha || alpha is 0 or byte.MaxValue ? channel : (byte)Math.Min(byte.MaxValue, channel * byte.MaxValue / alpha);
}
