// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Export;

namespace HyperPdf.Benchmarks;

/// <summary>Measures wrapping a picture file in a one page PDF, which is what opening a picture does; allocations come from the EventPipe trace.</summary>
public class PictureDocumentBenchmarks
{
    /// <summary>The picture's width in pixels.</summary>
    private const int Width = 640;

    /// <summary>The picture's height in pixels.</summary>
    private const int Height = 480;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of green in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The steps of the gradient.</summary>
    private const int Steps = 256;

    /// <summary>The PNG picture.</summary>
    private byte[] _png = null!;

    /// <summary>The BMP picture.</summary>
    private byte[] _bmp = null!;

    /// <summary>Makes a gradient picture in two file formats.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var pixels = new byte[Width * Height * BytesPerPixel];
        for (var offset = 0; offset < pixels.Length; offset += BytesPerPixel)
        {
            var pixel = offset / BytesPerPixel;
            pixels[offset] = (byte)(pixel % Width * Steps / Width);
            pixels[offset + GreenOffset] = (byte)(pixel / Width * Steps / Height);
            pixels[offset + RedOffset] = byte.MaxValue;
            pixels[offset + AlphaOffset] = byte.MaxValue;
        }

        var image = PdfRasterFiles.FromPremultipliedBgra(pixels, Width, Height);
        _png = PdfRasterFiles.Encode(image, PdfRasterFileFormat.Png, CancellationToken.None);
        _bmp = PdfRasterFiles.Encode(image, PdfRasterFileFormat.Bmp, CancellationToken.None);
    }

    /// <summary>Wraps a PNG picture.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int WrapPng() => PdfImageDocument.FromImageFile(_png, "picture.png", CancellationToken.None).Length;

    /// <summary>Wraps a BMP picture.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int WrapBmp() => PdfImageDocument.FromImageFile(_bmp, "picture.bmp", CancellationToken.None).Length;
}
