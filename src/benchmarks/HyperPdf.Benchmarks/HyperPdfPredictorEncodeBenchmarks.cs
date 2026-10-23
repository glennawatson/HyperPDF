// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Applies the TIFF and PNG predictors to a smooth three-component image at several bit depths.</summary>
public class HyperPdfPredictorEncodeBenchmarks
{
    /// <summary>The components in a pixel.</summary>
    private const int Colors = 3;

    /// <summary>The pixels in a row.</summary>
    private const int Columns = 1024;

    /// <summary>The rows.</summary>
    private const int Rows = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The seed of the noise added to the gradient.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The mask of the small noise added to each byte.</summary>
    private const uint NoiseMask = 3;

    /// <summary>The PNG predictor value that picks the best filter for each row.</summary>
    private const int OptimumPredictor = 15;

    /// <summary>The PNG predictor value for the Sub filter on every row.</summary>
    private const int SubPredictor = 11;

    /// <summary>The image rows.</summary>
    private byte[] _rows = [];

    /// <summary>The bytes in a row.</summary>
    private int _rowBytes;

    /// <summary>Gets or sets the bits per component.</summary>
    [Params(1, 8, 16)]
    public int Bits { get; set; } = ByteBits;

    /// <summary>Builds the image: a gradient with a little noise.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rowBytes = ((Columns * Colors * Bits) + ByteBits - 1) / ByteBits;
        _rows = new byte[_rowBytes * Rows];
        var state = Seed;
        for (var i = 0; i < _rows.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            _rows[i] = (byte)(((i % _rowBytes) >> 2) + (state & NoiseMask));
        }
    }

    /// <summary>Applies TIFF Predictor 2.</summary>
    /// <returns>The output length.</returns>
    [Benchmark]
    public int Tiff()
    {
        var output = default(PooledBuffer);
        try
        {
            TiffPredictorEncoder.Encode(_rows, _rowBytes, Colors, Bits, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Applies PNG Predictor 15, which tries all five filters on each row.</summary>
    /// <returns>The output length.</returns>
    [Benchmark(Baseline = true)]
    public int PngOptimum() => Png(OptimumPredictor);

    /// <summary>Applies PNG Predictor 11, the Sub filter on every row.</summary>
    /// <returns>The output length.</returns>
    [Benchmark]
    public int PngSub() => Png(SubPredictor);

    /// <summary>Applies a PNG predictor.</summary>
    /// <param name="predictor">The predictor value.</param>
    /// <returns>The output length.</returns>
    private int Png(int predictor)
    {
        var output = default(PooledBuffer);
        try
        {
            PngPredictorEncoder.Encode(_rows, _rowBytes, PngPredictorEncoder.GetPixelBytes(Colors, Bits), predictor, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
