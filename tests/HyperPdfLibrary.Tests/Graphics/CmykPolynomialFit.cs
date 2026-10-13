// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// Fits a degree-3 polynomial in cyan, magenta, yellow and black to <see cref="CmykNeugebauerModel"/> by least squares on a
/// dense grid. Each channel is <c>1 + sum(coefficient * term)</c>, so zero ink maps to exactly paper white. The terms are
/// the four inks, the ten products of two inks and the twenty products of three inks, each in nondecreasing ink order
/// (C, M, Y, K). The converter in the library holds the result as constants; a test regenerates it with this class.
/// </summary>
internal static class CmykPolynomialFit
{
    /// <summary>The terms of each channel.</summary>
    internal const int TermCount = 34;

    /// <summary>The index of yellow among the inks.</summary>
    internal const int YellowInk = 2;

    /// <summary>The index of black among the inks.</summary>
    internal const int BlackInk = 3;

    /// <summary>The grid points per ink of the fit.</summary>
    private const int FitGrid = 17;

    /// <summary>The channels.</summary>
    private const int Channels = 3;

    /// <summary>The inks.</summary>
    private const int Inks = CmykNeugebauerModel.InkCount;

    /// <summary>Fits the polynomial.</summary>
    /// <returns>The coefficients: one row of <see cref="TermCount"/> values for each of red, green and blue.</returns>
    internal static double[][] Fit()
    {
        var normal = new double[TermCount][];
        var right = new double[Channels][];
        for (var row = 0; row < TermCount; row++)
        {
            normal[row] = new double[TermCount];
        }

        for (var channel = 0; channel < Channels; channel++)
        {
            right[channel] = new double[TermCount];
        }

        var terms = new double[TermCount];
        for (var index = 0; index < FitGrid * FitGrid * FitGrid * FitGrid; index++)
        {
            var c = GridInk(index, 0, FitGrid);
            var m = GridInk(index, 1, FitGrid);
            var y = GridInk(index, YellowInk, FitGrid);
            var k = GridInk(index, BlackInk, FitGrid);
            Terms(c, m, y, k, terms);
            var target = CmykNeugebauerModel.ToSrgb(c, m, y, k);
            double[] goals = [target.Red - 1, target.Green - 1, target.Blue - 1];
            for (var row = 0; row < TermCount; row++)
            {
                for (var column = 0; column < TermCount; column++)
                {
                    normal[row][column] += terms[row] * terms[column];
                }

                for (var channel = 0; channel < Channels; channel++)
                {
                    right[channel][row] += terms[row] * goals[channel];
                }
            }
        }

        return [Solve(normal, right[0]), Solve(normal, right[1]), Solve(normal, right[2])];
    }

    /// <summary>Gets one ink of a grid point, where the grid is walked with cyan changing slowest and black fastest.</summary>
    /// <param name="index">The grid point index.</param>
    /// <param name="ink">The ink: 0 for cyan up to 3 for black.</param>
    /// <param name="grid">The grid points per ink.</param>
    /// <returns>The ink value from 0 to 1.</returns>
    internal static double GridInk(int index, int ink, int grid)
    {
        var place = 1;
        for (var step = Inks - 1; step > ink; step--)
        {
            place *= grid;
        }

        return ((index / place) % grid) / (grid - 1.0);
    }

    /// <summary>Evaluates the polynomial of one channel, unclamped.</summary>
    /// <param name="coefficients">The coefficients of the channel.</param>
    /// <param name="c">The cyan value.</param>
    /// <param name="m">The magenta value.</param>
    /// <param name="y">The yellow value.</param>
    /// <param name="k">The black value.</param>
    /// <returns>The channel value.</returns>
    internal static double Evaluate(double[] coefficients, double c, double m, double y, double k)
    {
        var terms = new double[TermCount];
        Terms(c, m, y, k, terms);
        var sum = 1.0;
        for (var term = 0; term < TermCount; term++)
        {
            sum += coefficients[term] * terms[term];
        }

        return sum;
    }

    /// <summary>Fills the terms in the documented order.</summary>
    /// <param name="c">The cyan value.</param>
    /// <param name="m">The magenta value.</param>
    /// <param name="y">The yellow value.</param>
    /// <param name="k">The black value.</param>
    /// <param name="terms">Receives the terms.</param>
    private static void Terms(double c, double m, double y, double k, double[] terms)
    {
        double[] ink = [c, m, y, k];
        var next = 0;
        for (var a = 0; a < Inks; a++)
        {
            terms[next] = ink[a];
            next++;
        }

        for (var a = 0; a < Inks; a++)
        {
            for (var b = a; b < Inks; b++)
            {
                terms[next] = ink[a] * ink[b];
                next++;
            }
        }

        for (var a = 0; a < Inks; a++)
        {
            for (var b = a; b < Inks; b++)
            {
                for (var d = b; d < Inks; d++)
                {
                    terms[next] = ink[a] * ink[b] * ink[d];
                    next++;
                }
            }
        }
    }

    /// <summary>Solves the normal equations by Gaussian elimination with partial pivoting.</summary>
    /// <param name="normal">The normal matrix; not changed.</param>
    /// <param name="right">The right-hand side.</param>
    /// <returns>The solution.</returns>
    private static double[] Solve(double[][] normal, double[] right)
    {
        var size = right.Length;
        var work = new double[size][];
        for (var row = 0; row < size; row++)
        {
            work[row] = new double[size + 1];
            Array.Copy(normal[row], work[row], size);
            work[row][size] = right[row];
        }

        for (var pivot = 0; pivot < size; pivot++)
        {
            var best = BestRow(work, pivot);
            var held = work[pivot];
            work[pivot] = work[best];
            work[best] = held;
            Eliminate(work, pivot);
        }

        var solution = new double[size];
        for (var row = 0; row < size; row++)
        {
            solution[row] = work[row][size] / work[row][row];
        }

        return solution;
    }

    /// <summary>Finds the row at or below the pivot with the largest entry in the pivot column.</summary>
    /// <param name="work">The augmented matrix.</param>
    /// <param name="pivot">The pivot row and column.</param>
    /// <returns>The row index.</returns>
    private static int BestRow(double[][] work, int pivot)
    {
        var best = pivot;
        for (var row = pivot + 1; row < work.Length; row++)
        {
            if (Math.Abs(work[row][pivot]) > Math.Abs(work[best][pivot]))
            {
                best = row;
            }
        }

        return best;
    }

    /// <summary>Clears the pivot column from every other row.</summary>
    /// <param name="work">The augmented matrix.</param>
    /// <param name="pivot">The pivot row and column.</param>
    private static void Eliminate(double[][] work, int pivot)
    {
        var width = work.Length;
        for (var row = 0; row < work.Length; row++)
        {
            if (row == pivot)
            {
                continue;
            }

            var factor = work[row][pivot] / work[pivot][pivot];
            for (var column = pivot; column <= width; column++)
            {
                work[row][column] -= factor * work[pivot][column];
            }
        }
    }
}
