// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Builds deterministic test pictures for the JPEG encoder: smooth colour gradients with a little texture.</summary>
internal static class JpegEncodeSamples
{
    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The parts an axis of the patch picture is split into.</summary>
    private const int Halves = 2;

    /// <summary>The strong channel of a patch.</summary>
    private const int Strong = 200;

    /// <summary>The weak channel of a patch.</summary>
    private const int Weak = 40;

    /// <summary>The middle channel of the last patch.</summary>
    private const int Mid = 60;

    /// <summary>The seed of the texture.</summary>
    private const uint Seed = 0xC0FFEE;

    /// <summary>The amplitude of the texture.</summary>
    private const int TextureRange = 13;

    /// <summary>The offset that centres the texture on zero.</summary>
    private const int TextureCentre = 6;

    /// <summary>The bytes of a four-byte pixel.</summary>
    private const int QuadBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The offset of the third byte of a pixel.</summary>
    private const int ThirdByte = 2;

    /// <summary>The offset of the fourth byte of a pixel.</summary>
    private const int FourthByte = 3;

    /// <summary>Gets the bytes of one pixel of a format.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The bytes.</returns>
    internal static int BytesPerPixel(PdfJpegPixelFormat format) => format switch
    {
        PdfJpegPixelFormat.Gray8 => 1,
        PdfJpegPixelFormat.Rgb24 => RgbBytes,
        _ => QuadBytes,
    };

    /// <summary>Builds a picture.</summary>
    /// <param name="format">The format of the pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The pixels, rows packed.</returns>
    internal static byte[] Pixels(PdfJpegPixelFormat format, int width, int height)
    {
        var random = new JpegTestRandom(Seed);
        var bytes = BytesPerPixel(format);
        var pixels = new byte[width * height * bytes];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var red = Scale(x, width) + random.Next(0, TextureRange) - TextureCentre;
                var green = Scale(y, height) + random.Next(0, TextureRange) - TextureCentre;
                var blue = Scale(x + y, width + height) + random.Next(0, TextureRange) - TextureCentre;
                Write(pixels.AsSpan(((y * width) + x) * bytes, bytes), format, Clamp(red), Clamp(green), Clamp(blue));
            }
        }

        return pixels;
    }

    /// <summary>Builds a picture of a few large flat patches, which survive chroma subsampling almost unchanged.</summary>
    /// <param name="format">The format of the pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The pixels, rows packed.</returns>
    internal static byte[] Patches(PdfJpegPixelFormat format, int width, int height)
    {
        var bytes = BytesPerPixel(format);
        var pixels = new byte[width * height * bytes];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var quadrant = (x < width / Halves ? 0 : 1) + (y < height / Halves ? 0 : Halves);
                var color = quadrant switch
                {
                    0 => new PatchColor(Strong, Weak, Weak),
                    1 => new PatchColor(Weak, Strong, Weak),
                    Halves => new PatchColor(Weak, Weak, Strong),
                    _ => new PatchColor(Strong, Strong, Mid),
                };
                Write(pixels.AsSpan(((y * width) + x) * bytes, bytes), format, color.Red, color.Green, color.Blue);
            }
        }

        return pixels;
    }

    /// <summary>Scales a position to a sample.</summary>
    /// <param name="position">The position.</param>
    /// <param name="length">The length that maps to 255.</param>
    /// <returns>The sample.</returns>
    private static int Scale(int position, int length) => position * MaxSample / Math.Max(1, length - 1);

    /// <summary>Clamps to a sample.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The byte.</returns>
    private static int Clamp(int value) => Math.Clamp(value, 0, MaxSample);

    /// <summary>Writes one pixel in a format.</summary>
    /// <param name="pixel">The pixel's bytes.</param>
    /// <param name="format">The format.</param>
    /// <param name="red">The red sample.</param>
    /// <param name="green">The green sample.</param>
    /// <param name="blue">The blue sample.</param>
    private static void Write(Span<byte> pixel, PdfJpegPixelFormat format, int red, int green, int blue)
    {
        switch (format)
        {
            case PdfJpegPixelFormat.Gray8:
            {
                pixel[0] = (byte)((red + green + blue) / RgbBytes);
                break;
            }

            case PdfJpegPixelFormat.Rgb24 or PdfJpegPixelFormat.Rgbx32:
            {
                pixel[0] = (byte)red;
                pixel[1] = (byte)green;
                pixel[ThirdByte] = (byte)blue;
                break;
            }

            case PdfJpegPixelFormat.Bgra32:
            {
                pixel[0] = (byte)blue;
                pixel[1] = (byte)green;
                pixel[ThirdByte] = (byte)red;
                pixel[FourthByte] = byte.MaxValue;
                break;
            }

            default:
            {
                // Ink amounts: cyan, magenta and yellow follow the colour, black follows the brightness.
                pixel[0] = (byte)(MaxSample - red);
                pixel[1] = (byte)(MaxSample - green);
                pixel[ThirdByte] = (byte)(MaxSample - blue);
                pixel[FourthByte] = (byte)((red + green + blue) / RgbBytes / TextureCentre);
                break;
            }
        }
    }

    /// <summary>One flat colour of the patch picture.</summary>
    /// <param name="Red">The red sample.</param>
    /// <param name="Green">The green sample.</param>
    /// <param name="Blue">The blue sample.</param>
    private readonly record struct PatchColor(int Red, int Green, int Blue);
}
