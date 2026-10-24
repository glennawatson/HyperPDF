// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Forms.Detection;
using HyperPdf.Core.Ocr;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the per-pixel row helpers that run over whole pages: luminance, stencil expansion and alpha; allocations come from the EventPipe trace.</summary>
public class PixelRowBenchmarks
{
    /// <summary>The pixels in a page-sized buffer.</summary>
    private const int Pixels = 2000 * 3000;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The width of a stencil row in pixels: a 600 dpi A4 page.</summary>
    private const int StencilWidth = 4960;

    /// <summary>The pixels of an alpha test image row set.</summary>
    private const int AlphaWidth = 2000;

    /// <summary>The rows of the alpha test image.</summary>
    private const int AlphaRows = 1000;

    /// <summary>The first row of the translucent band.</summary>
    private const int TranslucentStart = AlphaRows / 2;

    /// <summary>The rows in the translucent band.</summary>
    private const int TranslucentRows = 10;

    /// <summary>The alpha of the translucent band.</summary>
    private const byte TranslucentAlpha = 128;

    /// <summary>A multiplier that spreads byte values in the generated pixels.</summary>
    private const int Spread = 31;

    /// <summary>The page pixels.</summary>
    private byte[] _bgra = null!;

    /// <summary>The luminance plane.</summary>
    private byte[] _luma = null!;

    /// <summary>The packed stencil row.</summary>
    private byte[] _stencilRow = null!;

    /// <summary>The coverage row.</summary>
    private byte[] _coverage = null!;

    /// <summary>The pixels the alpha is applied to.</summary>
    private byte[] _alphaPixels = null!;

    /// <summary>The alpha plane, mostly opaque with a translucent band.</summary>
    private byte[] _alpha = null!;

    /// <summary>Fills the buffers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _bgra = new byte[Pixels * BytesPerPixel];
        for (var i = 0; i < _bgra.Length; i++)
        {
            _bgra[i] = (byte)(i * Spread);
        }

        _luma = new byte[Pixels];
        _stencilRow = new byte[(StencilWidth + BitsPerByte - 1) / BitsPerByte];
        for (var i = 0; i < _stencilRow.Length; i++)
        {
            _stencilRow[i] = (byte)(i * Spread);
        }

        _coverage = new byte[StencilWidth];
        _alphaPixels = new byte[AlphaWidth * AlphaRows * BytesPerPixel];
        _alpha = new byte[AlphaWidth * AlphaRows];
        _alpha.AsSpan().Fill(byte.MaxValue);
        _alpha.AsSpan(AlphaWidth * TranslucentStart, AlphaWidth * TranslucentRows).Fill(TranslucentAlpha);
    }

    /// <summary>The form finder's scalar luminance pass.</summary>
    [Benchmark]
    public void FormFinderLuminance() => DarkPixels.ToLuminance(_bgra, _luma);

    /// <summary>The OCR runner's grey pass, which is the same maths.</summary>
    [Benchmark]
    public void OcrGrey() => OcrRunner.ToGrey(_bgra, _luma);

    /// <summary>Expands one 600 dpi stencil row.</summary>
    [Benchmark]
    public void ExpandStencilRow() => ImageAlpha.ExpandStencilRow(_stencilRow, _coverage, false);

    /// <summary>Premultiplies a 2 megapixel image by a mostly opaque alpha plane.</summary>
    [Benchmark]
    public void PremultiplyMostlyOpaque() => ImageAlpha.Multiply(_alphaPixels, _alpha);
}
