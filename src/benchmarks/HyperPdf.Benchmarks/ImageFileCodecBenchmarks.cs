// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdf.Benchmarks;

/// <summary>Measures decoding PNG and BMP picture files and encoding rendered pages as picture files; allocations come from the EventPipe trace.</summary>
public class ImageFileCodecBenchmarks
{
    /// <summary>The picture's width in pixels.</summary>
    private const int Width = 1024;

    /// <summary>The picture's height in pixels.</summary>
    private const int Height = 1024;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The steps of the gradient.</summary>
    private const int Steps = 256;

    /// <summary>The offset of green in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The rows at the top of the picture that are opaque.</summary>
    private const int OpaqueRows = Height / 2;

    /// <summary>The alpha of the translucent half of the picture.</summary>
    private const byte HalfAlpha = 128;

    /// <summary>The rendered page's premultiplied pixels.</summary>
    private byte[] _premultiplied = null!;

    /// <summary>The page as raster samples.</summary>
    private HyperPdfLibrary.Creation.PdfRasterImage _image = null!;

    /// <summary>A PNG with transparency.</summary>
    private byte[] _png = null!;

    /// <summary>A BMP.</summary>
    private byte[] _bmp = null!;

    /// <summary>Makes a gradient page that is opaque in its top half and translucent below.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _premultiplied = new byte[Width * Height * BytesPerPixel];
        for (var offset = 0; offset < _premultiplied.Length; offset += BytesPerPixel)
        {
            var pixel = offset / BytesPerPixel;
            var alpha = pixel / Width < OpaqueRows ? byte.MaxValue : HalfAlpha;
            _premultiplied[offset] = (byte)(pixel % Width * Steps / Width * alpha / byte.MaxValue);
            _premultiplied[offset + GreenOffset] = (byte)(pixel / Width * Steps / Height * alpha / byte.MaxValue);
            _premultiplied[offset + RedOffset] = alpha;
            _premultiplied[offset + AlphaOffset] = alpha;
        }

        _image = PdfRasterFiles.FromPremultipliedBgra(_premultiplied, Width, Height);
        _png = PdfRasterFiles.Encode(_image, PdfRasterFileFormat.Png, CancellationToken.None);
        _bmp = PdfRasterFiles.Encode(_image, PdfRasterFileFormat.Bmp, CancellationToken.None);
    }

    /// <summary>Decodes the PNG.</summary>
    /// <returns>The pixel count.</returns>
    [Benchmark]
    public int DecodePng() => PdfImageFiles.Decode(_png, CancellationToken.None).Pixels.Length;

    /// <summary>Decodes the BMP.</summary>
    /// <returns>The pixel count.</returns>
    [Benchmark]
    public int DecodeBmp() => PdfImageFiles.Decode(_bmp, CancellationToken.None).Pixels.Length;

    /// <summary>Turns the rendered page's premultiplied pixels into straight samples.</summary>
    /// <returns>The pixel count.</returns>
    [Benchmark]
    public int PremultipliedToStraight() => PdfRasterFiles.FromPremultipliedBgra(_premultiplied, Width, Height).Pixels.Length;

    /// <summary>Encodes the page as a PNG.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int EncodePng() => PdfRasterFiles.Encode(_image, PdfRasterFileFormat.Png, CancellationToken.None).Length;

    /// <summary>Encodes the page as a BMP.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int EncodeBmp() => PdfRasterFiles.Encode(_image, PdfRasterFileFormat.Bmp, CancellationToken.None).Length;

    /// <summary>Encodes the page as a JPEG, which blends it over white first.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int EncodeJpeg() => PdfRasterFiles.Encode(_image, PdfRasterFileFormat.Jpeg, CancellationToken.None).Length;
}
