// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Tests.Graphics.Jpx;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Decodes full scanned pages with the managed JPEG 2000 decoder: a 5/3 lossless page and a 9/7 page, each through the
/// whole PDF path to BGRA, and the 5/3 page to component planes only. The 5/3 page is also re-encoded losslessly as
/// HTJ2K (ITU-T Rec. T.814) by the test encoder, once with the cleanup pass alone and once with the refinement passes,
/// and decoded the same ways.
/// </summary>
public class HyperPdfJpxBenchmarks
{
    /// <summary>The bits per sample of the pages.</summary>
    private const int SampleBits = 8;

    /// <summary>The decomposition levels of the HTJ2K pages.</summary>
    private const int HtLevels = 5;

    /// <summary>The code-block side of the HTJ2K pages as a power of two: 64.</summary>
    private const int HtBlockExponent = 6;

    /// <summary>The components of the pages.</summary>
    private const int Rgb = 3;

    /// <summary>The 5/3 page.</summary>
    private byte[] _reversible = [];

    /// <summary>The 5/3 page as HTJ2K, cleanup passes only.</summary>
    private byte[] _highThroughput = [];

    /// <summary>The 5/3 page as HTJ2K with SigProp and MagRef passes.</summary>
    private byte[] _highThroughputRefined = [];

    /// <summary>The 9/7 page.</summary>
    private byte[] _irreversible = [];

    /// <summary>The image header of the 5/3 page.</summary>
    private ImageHeader _reversibleHeader;

    /// <summary>The image header of the 9/7 page.</summary>
    private ImageHeader _irreversibleHeader;

    /// <summary>Loads the pages and reads their sizes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _reversible = Convert.FromBase64String(HyperPdfJpxPages.Reversible);
        _irreversible = Convert.FromBase64String(HyperPdfJpxPages.Irreversible);
        _reversibleHeader = HeaderOf(_reversible);
        _irreversibleHeader = HeaderOf(_irreversible);
        var planes = Planes(_reversible);
        var options = new JpxTestOptions
        {
            Width = _reversibleHeader.Width,
            Height = _reversibleHeader.Height,
            Components = Rgb,
            Transform = true,
            Levels = HtLevels,
            BlockExponent = HtBlockExponent,
            Style = JpxBlockStyle.HighThroughput,
        };
        _highThroughput = JpxTestEncoder.Encode(options, planes);
        _highThroughputRefined = JpxTestEncoder.Encode(options with { HtRefinement = true }, planes);
    }

    /// <summary>Decodes the HTJ2K page, cleanup passes only, to BGRA.</summary>
    /// <returns>The image.</returns>
    [Benchmark]
    public PdfImageData? HighThroughput() => JpxImageDecoder.Decode(_reversibleHeader, _highThroughput, 0);

    /// <summary>Decodes the HTJ2K page with refinement passes to BGRA.</summary>
    /// <returns>The image.</returns>
    [Benchmark]
    public PdfImageData? HighThroughputRefined() => JpxImageDecoder.Decode(_reversibleHeader, _highThroughputRefined, 0);

    /// <summary>Decodes the HTJ2K page, cleanup passes only, to component planes.</summary>
    /// <returns>The first sample.</returns>
    [Benchmark]
    public int HighThroughputPlanes() => FirstSample(_highThroughput);

    /// <summary>Decodes the 5/3 page to BGRA.</summary>
    /// <returns>The image.</returns>
    [Benchmark(Baseline = true)]
    public PdfImageData? Reversible() => JpxImageDecoder.Decode(_reversibleHeader, _reversible, 0);

    /// <summary>Decodes the 9/7 page to BGRA.</summary>
    /// <returns>The image.</returns>
    [Benchmark]
    public PdfImageData? Irreversible() => JpxImageDecoder.Decode(_irreversibleHeader, _irreversible, 0);

    /// <summary>Decodes the 5/3 page to component planes, without the colour conversion.</summary>
    /// <returns>The first sample.</returns>
    [Benchmark]
    public int ReversiblePlanes() => FirstSample(_reversible);

    /// <summary>Decodes a file to component planes and returns the first sample.</summary>
    /// <param name="data">The JP2 file or codestream.</param>
    /// <returns>The first sample.</returns>
    private static int FirstSample(byte[] data)
    {
        var file = JpxFileFormat.Read(data)!;
        var stream = data.AsSpan(file.Codestream.Offset, file.Codestream.Length);
        using var image = JpxDecoder.Decode(JpxCodestream.Read(stream)!, stream);
        return image.Planes[0][0];
    }

    /// <summary>Decodes a file to whole component planes.</summary>
    /// <param name="data">The JP2 file.</param>
    /// <returns>Each component's samples.</returns>
    private static int[][] Planes(byte[] data)
    {
        var file = JpxFileFormat.Read(data)!;
        var stream = data.AsSpan(file.Codestream.Offset, file.Codestream.Length);
        using var image = JpxDecoder.Decode(JpxCodestream.Read(stream)!, stream);
        var planes = new int[image.Planes.Length][];
        for (var c = 0; c < planes.Length; c++)
        {
            planes[c] = image.Planes[c].AsSpan(0, image.Areas[c].Width * image.Areas[c].Height).ToArray();
        }

        return planes;
    }

    /// <summary>Reads a page's size into a DeviceRGB image header.</summary>
    /// <param name="data">The JP2 file.</param>
    /// <returns>The header.</returns>
    private static ImageHeader HeaderOf(byte[] data)
    {
        var file = JpxFileFormat.Read(data)!;
        var geometry = JpxCodestream.Read(data.AsSpan(file.Codestream.Offset, file.Codestream.Length))!.Geometry;
        return new(geometry.Image.Width, geometry.Image.Height, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);
    }
}
