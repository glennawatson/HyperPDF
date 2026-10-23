// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>The PNG row filters written for <c>/Predictor 15</c> are undone exactly by the predictor decoder.</summary>
public sealed class PngPredictorEncoderTests
{
    /// <summary>The width of the test image in pixels.</summary>
    private const int Width = 37;

    /// <summary>The height of the test image in pixels.</summary>
    private const int Height = 23;

    /// <summary>The multiplier that spreads the test pattern.</summary>
    private const int Spread = 7;

    /// <summary>The shift that mixes the row into the pattern.</summary>
    private const int RowShift = 3;

    /// <summary>The predictor value that lets the encoder pick a filter for each row.</summary>
    private const int OptimumPredictor = 15;

    /// <summary>The bits of each sample.</summary>
    private const int Bits = 8;

    /// <summary>The entries of a parameter dictionary.</summary>
    private const int ParameterEntries = 4;

    /// <summary>Gray, RGB and four component rows survive the filters and their decoder.</summary>
    /// <param name="colors">The components of a pixel.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task RowsRoundTripThroughThePredictorDecoder(int colors)
    {
        var samples = CreateSamples(colors);

        var restored = FilterAndUndo(samples, colors);

        await Assert.That(restored).IsEquivalentTo(samples);
    }

    /// <summary>Smooth gradients pick filters that make the rows smaller than the plain rows once compressed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GradientsCompressBetterThanPlainRows()
    {
        var samples = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                samples[(y * Width) + x] = (byte)(x + (y * Spread));
            }
        }

        var (plain, filtered) = CompressedSizes(samples);

        await Assert.That(filtered).IsLessThan(plain);
    }

    /// <summary>A sample run that is not a whole number of rows is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialRowsAreRefused()
    {
        var threw = EncodeRefused(new byte[Width + 1]);

        await Assert.That(threw).IsTrue();
    }

    /// <summary>Filters the samples and undoes the filters with the predictor decoder.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="colors">The components of a pixel.</param>
    /// <returns>The restored samples.</returns>
    private static byte[] FilterAndUndo(byte[] samples, int colors)
    {
        var encoded = default(PooledBuffer);
        try
        {
            PngPredictorEncoder.Encode(samples, Width * colors, PngPredictorEncoder.GetPixelBytes(colors, Bits), ref encoded);
            var parameters = new PdfDictionary(null, ParameterEntries);
            parameters.Set(KnownName.Predictor, PdfValue.FromInteger(OptimumPredictor));
            parameters.Set(KnownName.Colors, PdfValue.FromInteger(colors));
            parameters.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(Bits));
            parameters.Set(KnownName.Columns, PdfValue.FromInteger(Width));
            PredictorFilter.Apply(parameters, ref encoded);
            return encoded.WrittenSpan.ToArray();
        }
        finally
        {
            encoded.Dispose();
        }
    }

    /// <summary>Compresses single-component rows plain and filtered.</summary>
    /// <param name="samples">The samples.</param>
    /// <returns>The compressed size of the plain rows and of the filtered rows.</returns>
    private static SizePair CompressedSizes(byte[] samples)
    {
        var encoded = default(PooledBuffer);
        var plain = default(PooledBuffer);
        var filtered = default(PooledBuffer);
        try
        {
            PngPredictorEncoder.Encode(samples, Width, 1, ref encoded);
            FlateFilter.Encode(samples, ref plain);
            FlateFilter.Encode(encoded.WrittenSpan, ref filtered);
            return new(plain.Length, filtered.Length);
        }
        finally
        {
            encoded.Dispose();
            plain.Dispose();
            filtered.Dispose();
        }
    }

    /// <summary>Tries to filter samples that are not a whole number of rows.</summary>
    /// <param name="samples">The samples.</param>
    /// <returns><see langword="true"/> when the encoder refused them.</returns>
    private static bool EncodeRefused(byte[] samples)
    {
        var output = default(PooledBuffer);
        try
        {
            PngPredictorEncoder.Encode(samples, Width, 1, ref output);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Makes a deterministic pattern with horizontal and vertical structure.</summary>
    /// <param name="colors">The components of a pixel.</param>
    /// <returns>The samples.</returns>
    private static byte[] CreateSamples(int colors)
    {
        var samples = new byte[Width * Height * colors];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)((i * Spread) ^ (i >> RowShift));
        }

        return samples;
    }

    /// <summary>The compressed sizes of the same rows plain and filtered.</summary>
    /// <param name="Plain">The size of the plain rows.</param>
    /// <param name="Filtered">The size of the filtered rows.</param>
    private readonly record struct SizePair(int Plain, int Filtered);
}
