// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Encodes and decodes a text-like one-bit page (200 dpi A4) as CCITT Group 4, Group 3 one-dimensional and Group 3 mixed.</summary>
public class HyperPdfCcittEncodeBenchmarks
{
    /// <summary>The page width in pixels.</summary>
    private const int Width = 1654;

    /// <summary>The page height in pixels.</summary>
    private const int Height = 2339;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes in a row of the page.</summary>
    private const int Stride = (Width + ByteBits - 1) / ByteBits;

    /// <summary>The distance between text lines in pixels.</summary>
    private const int LineSpacing = 48;

    /// <summary>The height of a text line in pixels.</summary>
    private const int LineHeight = 24;

    /// <summary>The left margin in pixels.</summary>
    private const int Margin = 100;

    /// <summary>The seed of the glyph widths.</summary>
    private const uint Seed = 0x1B873593;

    /// <summary>The widest glyph in pixels.</summary>
    private const int MaxGlyph = 12;

    /// <summary>The widest gap between glyphs in pixels.</summary>
    private const int MaxGap = 8;

    /// <summary>The distance between the starts of two glyph slots in pixels.</summary>
    private const int GlyphPitch = MaxGlyph + MaxGap + 1;

    /// <summary>The K value of the mixed scheme.</summary>
    private const int MixedK = 4;

    /// <summary>The page rows, one bit per pixel, set where the page is black.</summary>
    private byte[] _rows = [];

    /// <summary>The page as Group 4.</summary>
    private byte[] _group4 = [];

    /// <summary>The page as Group 3 one-dimensional.</summary>
    private byte[] _group3 = [];

    /// <summary>The scratch cache the encoders borrow from.</summary>
    private ScratchPools _scratch = new();

    /// <summary>The decoded rows, reused between runs.</summary>
    private byte[] _decoded = [];

    /// <summary>Builds the page and the encoded copies the decoders read.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _scratch = new();
        _rows = Page();
        _decoded = new byte[_rows.Length];
        _group4 = Encode(new(-1, Width, Height, true, false, false, true));
        _group3 = Encode(new(0, Width, Height, true, false, false, true));
    }

    /// <summary>Encodes the page as Group 4 with an end-of-block code.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark(Baseline = true)]
    public int EncodeGroup4() => EncodedLength(new(-1, Width, Height, true, false, false, true));

    /// <summary>Encodes the page as Group 3 one-dimensional.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int EncodeGroup3OneDimensional() => EncodedLength(new(0, Width, Height, true, false, false, true));

    /// <summary>Encodes the page as Group 3 mixed with K of 4, end-of-line codes and byte alignment.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int EncodeGroup3Mixed() => EncodedLength(new(MixedK, Width, Height, true, true, true, true));

    /// <summary>Decodes the Group 4 page.</summary>
    /// <returns>The first decoded byte.</returns>
    [Benchmark]
    public byte DecodeGroup4()
    {
        CcittFaxDecoder.Decode(_group4, new(-1, Width, Height, true, false, false, true), Width, Height, _decoded, _scratch);
        return _decoded[0];
    }

    /// <summary>Decodes the Group 3 one-dimensional page.</summary>
    /// <returns>The first decoded byte.</returns>
    [Benchmark]
    public byte DecodeGroup3OneDimensional()
    {
        CcittFaxDecoder.Decode(_group3, new(0, Width, Height, true, false, false, true), Width, Height, _decoded, _scratch);
        return _decoded[0];
    }

    /// <summary>Makes a page of text lines made of glyph boxes.</summary>
    /// <returns>The packed rows.</returns>
    private static byte[] Page()
    {
        var rows = new byte[Stride * Height];
        var random = new SampleRandom(Seed);
        for (var top = Margin; top + LineHeight < Height; top += LineSpacing)
        {
            for (var x = Margin; x < Width - Margin; x += GlyphPitch)
            {
                DrawGlyph(rows, x + random.Next(MaxGap), top, random);
            }
        }

        return rows;
    }

    /// <summary>Draws one glyph box of random width on a text line.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="x">The left edge of the glyph.</param>
    /// <param name="top">The top of the text line.</param>
    /// <param name="random">The repeatable generator.</param>
    private static void DrawGlyph(byte[] rows, int x, int top, SampleRandom random)
    {
        var glyph = 1 + random.Next(MaxGlyph);
        for (var y = top; y < top + LineHeight; y++)
        {
            for (var dx = 0; dx < glyph && x + dx < Width; dx++)
            {
                rows[(y * Stride) + ((x + dx) / ByteBits)] |= (byte)(1 << (ByteBits - 1 - ((x + dx) % ByteBits)));
            }
        }
    }

    /// <summary>Encodes the page and copies the result out, for the setup that prepares decoder input.</summary>
    /// <param name="parameters">The scheme and options.</param>
    /// <returns>The encoded bytes.</returns>
    private byte[] Encode(CcittParameters parameters)
    {
        var output = default(PooledBuffer);
        try
        {
            CcittEncoder.Encode(_rows, Width, Height, parameters, ref output, _scratch);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the page without copying the result.</summary>
    /// <param name="parameters">The scheme and options.</param>
    /// <returns>The encoded length.</returns>
    private int EncodedLength(CcittParameters parameters)
    {
        var output = default(PooledBuffer);
        try
        {
            CcittEncoder.Encode(_rows, Width, Height, parameters, ref output, _scratch);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
