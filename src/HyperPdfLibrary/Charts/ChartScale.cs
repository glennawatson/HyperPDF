// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Charts;

/// <summary>A value axis that starts and ends on round numbers with evenly spaced ticks.</summary>
/// <param name="Minimum">The value at the start of the axis.</param>
/// <param name="Maximum">The value at the end of the axis.</param>
/// <param name="Step">The distance between ticks.</param>
internal readonly record struct ChartScale(double Minimum, double Maximum, double Step)
{
    /// <summary>The tick count aimed for.</summary>
    private const int TargetTicks = 6;

    /// <summary>The factor between decades.</summary>
    private const double Decade = 10D;

    /// <summary>The share of an empty range added to each side.</summary>
    private const double FlatRangeSpread = 0.1D;

    /// <summary>The values at which a step rounds up to the next of <see cref="RoundedSteps"/>.</summary>
    private static readonly double[] RoundBreaks = [1.5D, 3D, 7D];

    /// <summary>The round multiples of a decade a step may be.</summary>
    private static readonly double[] RoundedSteps = [1D, 2D, 5D, 10D];

    /// <summary>Gets the number of steps from the start to the end.</summary>
    internal int StepCount => (int)Math.Round((Maximum - Minimum) / Step);

    /// <summary>Makes an axis that covers some values.</summary>
    /// <param name="lowest">The lowest value to show.</param>
    /// <param name="highest">The highest value to show.</param>
    /// <returns>The axis.</returns>
    internal static ChartScale Create(double lowest, double highest)
    {
        if (highest <= lowest && lowest == 0D)
        {
            highest = 1D;
        }
        else if (highest <= lowest)
        {
            var spread = Math.Abs(lowest) * FlatRangeSpread;
            highest = lowest + spread;
            lowest -= spread;
        }

        var step = RoundStep((highest - lowest) / (TargetTicks - 1));
        return new(Math.Floor(lowest / step) * step, Math.Ceiling(highest / step) * step, step);
    }

    /// <summary>Gets the value at a tick.</summary>
    /// <param name="index">The tick, from 0 at the start of the axis.</param>
    /// <returns>The value.</returns>
    internal double GetTick(int index) => Minimum + (index * Step);

    /// <summary>Gets how far along the axis a value is.</summary>
    /// <param name="value">The value.</param>
    /// <returns>0 at the start of the axis and 1 at the end.</returns>
    internal double GetFraction(double value) => (value - Minimum) / (Maximum - Minimum);

    /// <summary>Rounds a step up or down to 1, 2 or 5 times a power of ten.</summary>
    /// <param name="raw">The step.</param>
    /// <returns>The round step.</returns>
    private static double RoundStep(double raw)
    {
        var magnitude = Math.Pow(Decade, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var choice = 0;
        while (choice < RoundBreaks.Length && fraction >= RoundBreaks[choice])
        {
            choice++;
        }

        return RoundedSteps[choice] * magnitude;
    }
}
