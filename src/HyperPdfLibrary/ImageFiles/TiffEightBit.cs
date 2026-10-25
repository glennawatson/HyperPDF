// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>
/// Makes a picture from a TIFF whose samples are eight bits and stored pixel by pixel, the common case. The strips' bytes
/// already are the picture's bytes, so grey and RGB are used as they are, RGB with alpha has its red and blue swapped sixteen
/// bytes at a time, and a palette is turned into colours by a 256 entry table.
/// </summary>
internal static class TiffEightBit
{
    /// <summary>The photometric value where zero is white.</summary>
    private const int WhiteIsZero = 0;

    /// <summary>The photometric value where zero is black.</summary>
    private const int BlackIsZero = 1;

    /// <summary>The photometric value for red, green and blue.</summary>
    private const int Rgb = 2;

    /// <summary>The photometric value for a palette.</summary>
    private const int Palette = 3;

    /// <summary>The meaning of an extra sample that is premultiplied alpha.</summary>
    private const int AssociatedAlpha = 1;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The entries in the palette of eight-bit indexes.</summary>
    private const int Entries = 256;

    /// <summary>The offset of the third sample of a pixel.</summary>
    private const int ThirdIndex = 2;

    /// <summary>Determines whether a picture's samples can be used as they are or by this class's conversions.</summary>
    /// <param name="info">What the tags say.</param>
    /// <returns><see langword="true"/> for eight bit samples stored pixel by pixel in a layout this class handles.</returns>
    internal static bool Handles(in TiffInfo info)
    {
        if (info.BitsPerSample != ByteBits || info.Separate)
        {
            return false;
        }

        return info.Photometric switch
        {
            WhiteIsZero or BlackIsZero or Palette => info.SamplesPerPixel == 1,
            Rgb => info.SamplesPerPixel == RgbBytes || (info.SamplesPerPixel == BgraBytes && info.Alpha != 0 && info.Alpha != AssociatedAlpha),
            _ => false,
        };
    }

    /// <summary>Makes the picture.</summary>
    /// <param name="info">What the tags say; <see cref="Handles"/> is true for it.</param>
    /// <param name="bytes">The samples, pixel by pixel, one byte each.</param>
    /// <param name="colorMap">The palette tag's values: all the reds, then all the greens, then all the blues.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The palette is missing or too short.</exception>
    internal static PdfRasterImage Convert(in TiffInfo info, byte[] bytes, uint[] colorMap)
    {
        switch (info.Photometric)
        {
            case WhiteIsZero:
            {
                RowInvert.Invert(bytes);
                return new(PdfRasterFormat.Gray8, info.Width, info.Height, bytes);
            }

            case BlackIsZero:
            {
                return new(PdfRasterFormat.Gray8, info.Width, info.Height, bytes);
            }

            case Palette:
            {
                return Indexed(info, bytes, colorMap);
            }

            default:
            {
                if (info.SamplesPerPixel == RgbBytes)
                {
                    return new(PdfRasterFormat.Rgb24, info.Width, info.Height, bytes);
                }

                // The samples were read for this picture alone, so red and blue are swapped in place.
                PngEightBit.SwapRedAndBlue(bytes);
                return new(PdfRasterFormat.Bgra32, info.Width, info.Height, bytes);
            }
        }
    }

    /// <summary>Makes a colour picture from palette indexes.</summary>
    /// <param name="info">What the tags say.</param>
    /// <param name="indexes">The index of every pixel.</param>
    /// <param name="colorMap">All the reds, then all the greens, then all the blues, 16 bits each.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The palette is missing or too short.</exception>
    private static PdfRasterImage Indexed(in TiffInfo info, byte[] indexes, uint[] colorMap)
    {
        if (colorMap.Length < Entries * RgbBytes)
        {
            throw new InvalidDataException("The TIFF's palette is missing or too short.");
        }

        var output = new byte[indexes.Length * RgbBytes];
        for (var i = 0; i < indexes.Length; i++)
        {
            var index = indexes[i];
            var at = i * RgbBytes;
            output[at] = (byte)(colorMap[index] >> ByteBits);
            output[at + 1] = (byte)(colorMap[Entries + index] >> ByteBits);
            output[at + ThirdIndex] = (byte)(colorMap[Entries + Entries + index] >> ByteBits);
        }

        return new(PdfRasterFormat.Rgb24, info.Width, info.Height, output);
    }
}
