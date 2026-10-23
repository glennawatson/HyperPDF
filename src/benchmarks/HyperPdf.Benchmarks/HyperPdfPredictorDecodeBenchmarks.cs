// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Undoes the PNG and TIFF predictors of a smooth three-component 8-bit image, one filter for every row.</summary>
public class HyperPdfPredictorDecodeBenchmarks
{
    /// <summary>The components in a pixel.</summary>
    private const int Colors = 3;

    /// <summary>The pixels in a row.</summary>
    private const int Columns = 1024;

    /// <summary>The rows.</summary>
    private const int Rows = 512;

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

    /// <summary>The TIFF horizontal predictor.</summary>
    private const int TiffPredictor = 2;

    /// <summary>The PNG predictor value for the Sub filter on every row.</summary>
    private const int SubPredictor = 11;

    /// <summary>The PNG predictor value for the Up filter on every row.</summary>
    private const int UpPredictor = 12;

    /// <summary>The PNG predictor value for the Average filter on every row.</summary>
    private const int AveragePredictor = 13;

    /// <summary>The PNG predictor value for the Paeth filter on every row.</summary>
    private const int PaethPredictor = 14;

    /// <summary>The encoded image by predictor value.</summary>
    private readonly Dictionary<int, byte[]> _encoded = [];

    /// <summary>The decode parameters by predictor value.</summary>
    private readonly Dictionary<int, PdfDictionary> _parameters = [];

    /// <summary>Builds the image and encodes it with each predictor.</summary>
    [GlobalSetup]
    public void Setup()
    {
        const int rowBytes = Columns * Colors;
        var rows = new byte[rowBytes * Rows];
        var state = Seed;
        for (var i = 0; i < rows.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            rows[i] = (byte)(((i % rowBytes) >> 2) + (state & NoiseMask));
        }

        foreach (var predictor in new[] { SubPredictor, UpPredictor, AveragePredictor, PaethPredictor })
        {
            var output = default(PooledBuffer);
            try
            {
                PngPredictorEncoder.Encode(rows, rowBytes, Colors, predictor, ref output);
                _encoded[predictor] = output.ToArray();
            }
            finally
            {
                output.Dispose();
            }

            _parameters[predictor] = Parameters(predictor);
        }

        var tiff = default(PooledBuffer);
        try
        {
            TiffPredictorEncoder.Encode(rows, rowBytes, Colors, ByteBits, ref tiff);
            _encoded[TiffPredictor] = tiff.ToArray();
        }
        finally
        {
            tiff.Dispose();
        }

        _parameters[TiffPredictor] = Parameters(TiffPredictor);
    }

    /// <summary>Undoes the Sub filter on every row.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int Sub() => Decode(SubPredictor);

    /// <summary>Undoes the Up filter on every row.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark(Baseline = true)]
    public int Up() => Decode(UpPredictor);

    /// <summary>Undoes the Average filter on every row.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int Average() => Decode(AveragePredictor);

    /// <summary>Undoes the Paeth filter on every row.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int Paeth() => Decode(PaethPredictor);

    /// <summary>Undoes TIFF Predictor 2.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int Tiff() => Decode(TiffPredictor);

    /// <summary>Makes the decode parameters for a predictor.</summary>
    /// <param name="predictor">The predictor value.</param>
    /// <returns>The parameters.</returns>
    private static PdfDictionary Parameters(int predictor)
    {
        var parameters = new PdfDictionary(null);
        parameters.Add(KnownName.Predictor, PdfValue.FromInteger(predictor));
        parameters.Add(KnownName.Colors, PdfValue.FromInteger(Colors));
        parameters.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
        parameters.Add(KnownName.Columns, PdfValue.FromInteger(Columns));
        return parameters;
    }

    /// <summary>Copies the encoded image into a buffer and undoes its predictor in place.</summary>
    /// <param name="predictor">The predictor value.</param>
    /// <returns>The decoded length.</returns>
    private int Decode(int predictor)
    {
        var buffer = default(PooledBuffer);
        try
        {
            buffer.Write(_encoded[predictor]);
            PredictorFilter.Apply(_parameters[predictor], ref buffer);
            return buffer.Length;
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
