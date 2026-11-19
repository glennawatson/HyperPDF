// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures giving a row of rendered pixels the page tone: a blank row, a row of text and a row of a picture.</summary>
public class PageToneApplyBenchmarks
{
    /// <summary>The pixels in a row, which is a tile as wide as a wide window.</summary>
    private const int Pixels = 1224;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>How far apart the clusters of text pixels start in a text row.</summary>
    private const int ClusterEvery = 40;

    /// <summary>The step between the greys of neighbouring pixels in a cluster.</summary>
    private const int GreyStep = 40;

    /// <summary>The grey of the first pixel in a cluster.</summary>
    private const int FirstGrey = 10;

    /// <summary>The pixels in a cluster of text.</summary>
    private const int ClusterLength = 6;

    /// <summary>The number that spreads the values of the picture row.</summary>
    private const uint Spread = 2_654_435_761U;

    /// <summary>The offset of the green byte in a pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red byte in a pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The bits the green channel of a colour is shifted by.</summary>
    private const int GreenShift = 8;

    /// <summary>The bits the red channel of a colour is shifted by.</summary>
    private const int RedShift = 16;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The tone, a dark paper with light ink.</summary>
    private readonly PageTone _tone = new(0x2A2826U, 0xD2CDC5U);

    /// <summary>The pixels of the row of blank paper, which are all white.</summary>
    private readonly byte[] _blank = new byte[Pixels * BytesPerPixel];

    /// <summary>The row of text: white with a cluster of dark and grey pixels every so often.</summary>
    private readonly byte[] _text = new byte[Pixels * BytesPerPixel];

    /// <summary>The row of a picture, where nearly every pixel is a different colour.</summary>
    private readonly byte[] _picture = new byte[Pixels * BytesPerPixel];

    /// <summary>The row the benchmark works on, refilled before each run so every run starts from the same pixels.</summary>
    private readonly byte[] _work = new byte[Pixels * BytesPerPixel];

    /// <summary>Makes the three rows.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _blank.AsSpan().Fill(byte.MaxValue);
        _text.AsSpan().Fill(byte.MaxValue);
        for (var pixel = 0; pixel < Pixels; pixel++)
        {
            var offset = pixel % ClusterEvery;
            if (offset < ClusterLength)
            {
                var grey = (byte)((offset * GreyStep) + FirstGrey);
                _text[pixel * BytesPerPixel] = grey;
                _text[(pixel * BytesPerPixel) + GreenOffset] = grey;
                _text[(pixel * BytesPerPixel) + RedOffset] = grey;
            }

            var color = (uint)(pixel + 1) * Spread;
            _picture[pixel * BytesPerPixel] = (byte)color;
            _picture[(pixel * BytesPerPixel) + GreenOffset] = (byte)(color >> GreenShift);
            _picture[(pixel * BytesPerPixel) + RedOffset] = (byte)(color >> RedShift);
            _picture[(pixel * BytesPerPixel) + AlphaOffset] = byte.MaxValue;
        }
    }

    /// <summary>Gives a row of blank paper the tone.</summary>
    /// <returns>The first byte of the row.</returns>
    [Benchmark(Baseline = true)]
    public byte BlankRow() => Run(_blank);

    /// <summary>Gives a row of text the tone.</summary>
    /// <returns>The first byte of the row.</returns>
    [Benchmark]
    public byte TextRow() => Run(_text);

    /// <summary>Gives a row of a picture the tone.</summary>
    /// <returns>The first byte of the row.</returns>
    [Benchmark]
    public byte PictureRow() => Run(_picture);

    /// <summary>Copies a row into the work row and gives it the tone.</summary>
    /// <param name="source">The row.</param>
    /// <returns>The first byte of the toned row.</returns>
    private byte Run(byte[] source)
    {
        source.AsSpan().CopyTo(_work);
        _tone.Apply(_work);
        return _work[0];
    }
}
