// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdf.Benchmarks;

/// <summary>Encodes a 2000 by 1500 image as PNG, as exporting an extracted image does, with and without transparency.</summary>
public class HyperPdfPngWriterBenchmarks
{
    /// <summary>The width of the image.</summary>
    private const int Width = 2000;

    /// <summary>The height of the image.</summary>
    private const int Height = 1500;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The index of the red byte in a pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The index of the alpha byte in a pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The mask of the small noise added to the blue channel.</summary>
    private const byte NoiseMask = 3;

    /// <summary>The opaque image.</summary>
    private PdfImageData _opaque = null!;

    /// <summary>The image with a partly transparent pixel.</summary>
    private PdfImageData _transparent = null!;

    /// <summary>Builds the images outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // A smooth image with a little noise, like a photograph, so the compression is not the whole cost.
        var opaque = new byte[Width * Height * PixelBytes];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * PixelBytes;
                opaque[offset] = (byte)((x >> 3) + (y >> 4));
                opaque[offset + 1] = (byte)((x >> 4) + (y >> 3));
                opaque[offset + RedIndex] = (byte)((x + y) >> 4);
                opaque[offset + AlphaIndex] = byte.MaxValue;
            }
        }

        var noise = new byte[opaque.Length];
        RandomNumberGenerator.Fill(noise);
        for (var i = 0; i < opaque.Length; i += PixelBytes)
        {
            opaque[i] += (byte)(noise[i] & NoiseMask);
        }

        var transparent = (byte[])opaque.Clone();
        transparent[^1] = 0;
        _opaque = new(Width, Height, opaque, false, false, PdfImageCodec.None);
        _transparent = new(Width, Height, transparent, false, false, PdfImageCodec.None);
    }

    /// <summary>Encodes the opaque image.</summary>
    /// <returns>The PNG length.</returns>
    [Benchmark]
    public int EncodeOpaque() => PdfPngWriter.Encode(_opaque).Length;

    /// <summary>Encodes the image whose last pixel is transparent, so every pixel's alpha is scanned.</summary>
    /// <returns>The PNG length.</returns>
    [Benchmark]
    public int EncodeWithAlpha() => PdfPngWriter.Encode(_transparent).Length;
}
