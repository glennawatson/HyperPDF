// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the pixel conversions and block averaging the image optimizer runs on every recoded colour image.</summary>
public class HyperPdfImagePixelsBenchmarks
{
    /// <summary>The width of the sample image.</summary>
    private const int Width = 2000;

    /// <summary>The height of the sample image.</summary>
    private const int Height = 1500;

    /// <summary>The block side the image is reduced by.</summary>
    private const int Factor = 4;

    /// <summary>The components of an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The RGB samples.</summary>
    private byte[] _rgb = [];

    /// <summary>The RGBX pixels.</summary>
    private byte[] _rgbx = [];

    /// <summary>The grey pixels.</summary>
    private byte[] _grey = [];

    /// <summary>Builds the images outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rgb = new byte[Width * Height * Rgb];
        _rgbx = new byte[Width * Height * ImagePixels.RgbxBytes];
        _grey = new byte[Width * Height];
        RandomNumberGenerator.Fill(_rgb);
        RandomNumberGenerator.Fill(_rgbx);
        RandomNumberGenerator.Fill(_grey);
    }

    /// <summary>Widens RGB samples to RGBX pixels.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int WidenRgbToRgbx()
    {
        var pixels = default(PooledBuffer);
        try
        {
            ImagePixels.FromSamples(_rgb, Rgb, Width * Height, ref pixels);
            return pixels.Length;
        }
        finally
        {
            pixels.Dispose();
        }
    }

    /// <summary>Narrows RGBX pixels to RGB samples.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int NarrowRgbxToRgb()
    {
        var samples = default(PooledBuffer);
        try
        {
            ImagePixels.ToSamples(_rgbx, Rgb, Width * Height, ref samples);
            return samples.Length;
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>Averages 4 by 4 blocks of an RGBX image.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int ReduceRgbxBlocks() => Reduce(_rgbx, Rgb);

    /// <summary>Averages 4 by 4 blocks of a grey image.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int ReduceGreyBlocks() => Reduce(_grey, 1);

    /// <summary>Reduces an image by whole blocks; the target is the reduced size, so no resample runs.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="components">One for grey, three for RGBX.</param>
    /// <returns>The bytes written.</returns>
    private static int Reduce(byte[] pixels, int components)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = ImagePixels.Downsample(null!, pixels, new(Width, Height, components), Width / Factor, Height / Factor, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
