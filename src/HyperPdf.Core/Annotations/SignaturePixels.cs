// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Annotations;

/// <summary>Prepares straight-alpha BGRA signature pixels for placement.</summary>
public static class SignaturePixels
{
    /// <summary>The number of bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The alpha channel's byte offset.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The green channel's byte offset.</summary>
    private const int GreenOffset = 1;

    /// <summary>The red channel's byte offset.</summary>
    private const int RedOffset = 2;

    /// <summary>The largest channel value.</summary>
    private const int Opaque = 255;

    /// <summary>The bits of the alpha byte in a pixel read as one number, on this machine's byte order.</summary>
    private static readonly uint AlphaBits = BitConverter.IsLittleEndian ? 0xFF000000U : 0x000000FFU;

    /// <summary>Converts premultiplied BGRA pixels, as decoders produce them, to the straight alpha signatures store.</summary>
    /// <param name="pixels">Tightly packed BGRA pixels. The buffer is updated in place.</param>
    /// <exception cref="ArgumentException">The buffer ends with an incomplete pixel.</exception>
    public static void Unpremultiply(Span<byte> pixels)
    {
        ThrowIfPartial(pixels);
        var words = MemoryMarshal.Cast<byte, uint>(pixels);
        var done = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var width = Vector<uint>.Count;
            var alphaBits = new Vector<uint>(AlphaBits);
            for (; done <= words.Length - width; done += width)
            {
                // A run of clear and opaque pixels is already straight.
                var alpha = new Vector<uint>(words.Slice(done, width)) & alphaBits;
                if (!Vector.EqualsAll(Vector.Equals(alpha, Vector<uint>.Zero) | Vector.Equals(alpha, alphaBits), Vector<uint>.AllBitsSet))
                {
                    UnpremultiplyEach(pixels.Slice(done * Channels, width * Channels));
                }
            }
        }

        UnpremultiplyEach(pixels[(done * Channels)..]);
    }

    /// <summary>Removes white paper while retaining soft ink edges. Leaves images with existing transparency unchanged.</summary>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels. The buffer is updated in place.</param>
    /// <exception cref="ArgumentException">The buffer ends with an incomplete pixel.</exception>
    public static void RemoveWhitePaper(Span<byte> pixels)
    {
        ThrowIfPartial(pixels);
        var words = MemoryMarshal.Cast<byte, uint>(pixels);
        if (!IsOpaque(words))
        {
            return;
        }

        var done = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var width = Vector<uint>.Count;
            for (; done <= words.Length - width; done += width)
            {
                // Plain white paper, most of a scan, becomes fully clear.
                var block = words.Slice(done, width);
                Vector<uint> value = new(block);
                if (Vector.EqualsAll(value, Vector<uint>.AllBitsSet))
                {
                    block.Clear();
                    continue;
                }

                RemovePaperEach(pixels.Slice(done * Channels, width * Channels));
            }
        }

        RemovePaperEach(pixels[(done * Channels)..]);
    }

    /// <summary>Rejects a buffer that ends with an incomplete pixel.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <exception cref="ArgumentException">The buffer ends with an incomplete pixel.</exception>
    private static void ThrowIfPartial(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length % Channels != 0)
        {
            throw new ArgumentException("The image must contain complete BGRA pixels.", nameof(pixels));
        }
    }

    /// <summary>Tells whether every pixel is opaque.</summary>
    /// <param name="words">The pixels, each read as one number.</param>
    /// <returns><see langword="true"/> when no pixel has transparency.</returns>
    private static bool IsOpaque(ReadOnlySpan<uint> words)
    {
        var done = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var width = Vector<uint>.Count;
            var alphaBits = new Vector<uint>(AlphaBits);
            for (; done <= words.Length - width; done += width)
            {
                if (!Vector.EqualsAll(new Vector<uint>(words.Slice(done, width)) & alphaBits, alphaBits))
                {
                    return false;
                }
            }
        }

        foreach (var word in words[done..])
        {
            if ((word & AlphaBits) != AlphaBits)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Unpremultiplies pixels one at a time.</summary>
    /// <param name="pixels">The pixels.</param>
    private static void UnpremultiplyEach(Span<byte> pixels)
    {
        ReadOnlySpan<byte> table = SignaturePixelTables.Unpremultiplied;
        for (var offset = 0; offset < pixels.Length; offset += Channels)
        {
            var alpha = pixels[offset + AlphaOffset];
            if (alpha is 0 or Opaque)
            {
                continue;
            }

            pixels[offset] = table[SignaturePixelTables.IndexOf(alpha, pixels[offset])];
            pixels[offset + GreenOffset] = table[SignaturePixelTables.IndexOf(alpha, pixels[offset + GreenOffset])];
            pixels[offset + RedOffset] = table[SignaturePixelTables.IndexOf(alpha, pixels[offset + RedOffset])];
        }
    }

    /// <summary>Removes the paper from pixels one at a time.</summary>
    /// <param name="pixels">The opaque pixels.</param>
    private static void RemovePaperEach(Span<byte> pixels)
    {
        ReadOnlySpan<byte> table = SignaturePixelTables.PaperRemoved;
        for (var offset = 0; offset < pixels.Length; offset += Channels)
        {
            var blue = pixels[offset];
            var green = pixels[offset + GreenOffset];
            var red = pixels[offset + RedOffset];
            var paper = Math.Min(blue, Math.Min(green, red));
            var alpha = Opaque - paper;
            if (alpha == 0)
            {
                pixels.Slice(offset, Channels).Clear();
                continue;
            }

            // Undo the white contribution before applying alpha on the destination page.
            pixels[offset] = table[SignaturePixelTables.IndexOf(alpha, blue - paper)];
            pixels[offset + GreenOffset] = table[SignaturePixelTables.IndexOf(alpha, green - paper)];
            pixels[offset + RedOffset] = table[SignaturePixelTables.IndexOf(alpha, red - paper)];
            pixels[offset + AlphaOffset] = (byte)alpha;
        }
    }
}
