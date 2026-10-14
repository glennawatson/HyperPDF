// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The L2 norms of the one-dimensional synthesis basis functions of the 9/7 wavelet (ISO 15444-1 F.3.8), at each
/// decomposition level and for the low-pass and the high-pass band. A coefficient error spreads into the image with that
/// norm, so a quantizer that wants the same image error from every sub-band uses a step proportional to one over the norm
/// of the band, the product of its horizontal and vertical norms.
/// </summary>
internal static class JpxSynthesisNorms
{
    /// <summary>The coefficients at the deepest level in the measuring signal.</summary>
    private const int MeasureBand = 32;

    /// <summary>The deepest level measured; deeper levels extend the last measured one.</summary>
    private const int MeasuredLevels = 10;

    /// <summary>The growth of a norm for each level beyond those measured: the square root of two.</summary>
    private const double LevelGrowth = 1.4142135623730951;

    /// <summary>The first lifting coefficient (equation F-9).</summary>
    private const double Alpha = -1.586134342059924;

    /// <summary>The second lifting coefficient.</summary>
    private const double Beta = -0.052980118572961;

    /// <summary>The third lifting coefficient.</summary>
    private const double Gamma = 0.882911075530934;

    /// <summary>The fourth lifting coefficient.</summary>
    private const double Delta = 0.443506852043971;

    /// <summary>The scale K.</summary>
    private const double Scale = 1.230174104914001;

    /// <summary>The distance between samples of the same kind in an interleaved line.</summary>
    private const int Pair = 2;

    /// <summary>The norms of the low-pass basis function, by level.</summary>
    private static readonly double[] LowNorms = Measure(high: false);

    /// <summary>The norms of the high-pass basis function, by level.</summary>
    private static readonly double[] HighNorms = Measure(high: true);

    /// <summary>Gets the norm of a basis function.</summary>
    /// <param name="high">Whether the coefficient is in the high-pass band; otherwise the low-pass band.</param>
    /// <param name="level">The decomposition level of the band, 1 for the finest.</param>
    /// <returns>The norm.</returns>
    internal static double Get(bool high, int level)
    {
        var norms = high ? HighNorms : LowNorms;
        return level <= MeasuredLevels ? norms[level] : norms[MeasuredLevels] * Math.Pow(LevelGrowth, level - MeasuredLevels);
    }

    /// <summary>Measures the norms of one kind of basis function at every level.</summary>
    /// <param name="high">Whether to measure the high-pass band.</param>
    /// <returns>The norms, indexed by level; entry zero is unused.</returns>
    private static double[] Measure(bool high)
    {
        var norms = new double[MeasuredLevels + 1];
        for (var level = 1; level <= MeasuredLevels; level++)
        {
            var low = new double[MeasureBand];
            var detail = new double[MeasureBand];
            (high ? detail : low)[MeasureBand / Pair] = 1;
            var signal = Synthesize(low, detail);
            for (var up = 1; up < level; up++)
            {
                signal = Synthesize(signal, new double[signal.Length]);
            }

            var energy = 0.0;
            foreach (var sample in signal)
            {
                energy += sample * sample;
            }

            norms[level] = Math.Sqrt(energy);
        }

        return norms;
    }

    /// <summary>Runs one level of the inverse transform (equations F-10 to F-14) on a line that starts on an even coordinate.</summary>
    /// <param name="low">The low-pass samples.</param>
    /// <param name="detail">The high-pass samples, as many as the low-pass ones.</param>
    /// <returns>The reconstructed line, twice as long.</returns>
    private static double[] Synthesize(double[] low, double[] detail)
    {
        var line = new double[low.Length * Pair];
        for (var i = 0; i < low.Length; i++)
        {
            line[i * Pair] = low[i] * Scale;
            line[(i * Pair) + 1] = detail[i] / Scale;
        }

        Lift(line, 0, -Delta);
        Lift(line, 1, -Gamma);
        Lift(line, 0, -Beta);
        Lift(line, 1, -Alpha);
        return line;
    }

    /// <summary>Adds the sum of the neighbours times a factor to every second sample, with whole-sample symmetric extension.</summary>
    /// <param name="line">The line, changed in place.</param>
    /// <param name="first">The first sample to change.</param>
    /// <param name="factor">The factor.</param>
    private static void Lift(double[] line, int first, double factor)
    {
        var count = line.Length;
        for (var i = first; i < count; i += Pair)
        {
            line[i] += (line[JpxWaveletLevel.Mirror(i - 1, count)] + line[JpxWaveletLevel.Mirror(i + 1, count)]) * factor;
        }
    }
}
