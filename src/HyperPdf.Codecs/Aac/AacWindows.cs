// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// The rising halves of the AAC windows, for the sine shape and the Kaiser-Bessel derived shape, long and short. The falling half is
/// the rising half read backwards. The tables are made once and never change.
/// </summary>
internal static class AacWindows
{
    /// <summary>The shape number of the sine window.</summary>
    internal const int Sine = 0;

    /// <summary>The shape number of the Kaiser-Bessel derived window.</summary>
    internal const int KaiserBessel = 1;

    /// <summary>The Kaiser alpha of the long window.</summary>
    private const double LongAlpha = 4;

    /// <summary>The Kaiser alpha of the short window.</summary>
    private const double ShortAlpha = 6;

    /// <summary>The smallest term of the Bessel series that still counts.</summary>
    private const double SeriesLimit = 1e-12;

    /// <summary>Half a step.</summary>
    private const double Half = 0.5;

    /// <summary>A quarter.</summary>
    private const double Quarter = 0.25;

    /// <summary>The rising halves of the long windows, by shape.</summary>
    private static readonly float[][] LongRise = [BuildSine(AacChannel.LongSize), BuildKaiser(AacChannel.LongSize, LongAlpha)];

    /// <summary>The rising halves of the short windows, by shape.</summary>
    private static readonly float[][] ShortRise = [BuildSine(AacChannel.ShortSize), BuildKaiser(AacChannel.ShortSize, ShortAlpha)];

    /// <summary>Gets the rising half of a long window.</summary>
    /// <param name="shape">The window shape.</param>
    /// <returns>1024 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float[] Long(int shape) => LongRise[shape];

    /// <summary>Gets the rising half of a short window.</summary>
    /// <param name="shape">The window shape.</param>
    /// <returns>128 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float[] Short(int shape) => ShortRise[shape];

    /// <summary>Builds the rising half of a sine window.</summary>
    /// <param name="size">The values in the half.</param>
    /// <returns>The values.</returns>
    private static float[] BuildSine(int size)
    {
        var window = new float[size];
        for (var index = 0; index < size; index++)
        {
            window[index] = (float)Math.Sin(Math.PI * (index + Half) / (size << 1));
        }

        return window;
    }

    /// <summary>Builds the rising half of a Kaiser-Bessel derived window.</summary>
    /// <param name="size">The values in the half.</param>
    /// <param name="alpha">The Kaiser alpha.</param>
    /// <returns>The values.</returns>
    private static float[] BuildKaiser(int size, double alpha)
    {
        var kaiser = new double[size + 1];
        var total = 0.0;
        for (var index = 0; index <= size; index++)
        {
            var ratio = ((double)(index + index) / size) - 1;
            kaiser[index] = Bessel(Math.PI * alpha * Math.Sqrt(1 - (ratio * ratio)));
            total += kaiser[index];
        }

        var window = new float[size];
        var sum = 0.0;
        for (var index = 0; index < size; index++)
        {
            sum += kaiser[index];
            window[index] = (float)Math.Sqrt(sum / total);
        }

        return window;
    }

    /// <summary>Computes the zeroth order modified Bessel function of the first kind.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The value.</returns>
    private static double Bessel(double x)
    {
        var term = 1.0;
        var sum = 1.0;
        var quarter = x * x * Quarter;
        for (var step = 1; term > SeriesLimit * sum; step++)
        {
            term *= quarter / ((double)step * step);
            sum += term;
        }

        return sum;
    }
}
