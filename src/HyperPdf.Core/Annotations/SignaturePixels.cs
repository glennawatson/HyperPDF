// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>Prepares straight-alpha BGRA signature pixels for placement.</summary>
public static class SignaturePixels
{
    /// <summary>The number of bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The alpha channel's byte offset.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The red channel's byte offset.</summary>
    private const int RedOffset = 2;

    /// <summary>The largest channel value.</summary>
    private const int Opaque = 255;

    /// <summary>Converts premultiplied BGRA pixels, as decoders produce them, to the straight alpha signatures store.</summary>
    /// <param name="pixels">Tightly packed BGRA pixels. The buffer is updated in place.</param>
    /// <exception cref="ArgumentException">The buffer ends with an incomplete pixel.</exception>
    public static void Unpremultiply(Span<byte> pixels)
    {
        if (pixels.Length % Channels != 0)
        {
            throw new ArgumentException("The image must contain complete BGRA pixels.", nameof(pixels));
        }

        for (var offset = 0; offset < pixels.Length; offset += Channels)
        {
            var alpha = pixels[offset + AlphaOffset];
            if (alpha is 0 or Opaque)
            {
                continue;
            }

            // Round to nearest so a premultiply and unpremultiply round trip keeps colours.
            var half = alpha >> 1;
            for (var channel = 0; channel < AlphaOffset; channel++)
            {
                pixels[offset + channel] = (byte)Math.Min(Opaque, ((pixels[offset + channel] * Opaque) + half) / alpha);
            }
        }
    }

    /// <summary>Removes white paper while retaining soft ink edges. Leaves images with existing transparency unchanged.</summary>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels. The buffer is updated in place.</param>
    /// <exception cref="ArgumentException">The buffer ends with an incomplete pixel.</exception>
    public static void RemoveWhitePaper(Span<byte> pixels)
    {
        if (pixels.Length % Channels != 0)
        {
            throw new ArgumentException("The image must contain complete BGRA pixels.", nameof(pixels));
        }

        for (var offset = AlphaOffset; offset < pixels.Length; offset += Channels)
        {
            if (pixels[offset] != Opaque)
            {
                return;
            }
        }

        for (var offset = 0; offset < pixels.Length; offset += Channels)
        {
            var pixel = pixels.Slice(offset, Channels);
            var paper = Math.Min(pixel[0], Math.Min(pixel[1], pixel[RedOffset]));
            var alpha = Opaque - paper;
            if (alpha == 0)
            {
                pixel.Clear();
                continue;
            }

            // Undo the white contribution before applying alpha on the destination page.
            for (var channel = 0; channel < AlphaOffset; channel++)
            {
                pixel[channel] = (byte)(((pixel[channel] - paper) * Opaque) / alpha);
            }

            pixel[AlphaOffset] = (byte)alpha;
        }
    }
}
