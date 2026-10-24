// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the batched multiplication against a plain loop for every shape of tile it uses.</summary>
public sealed class MatrixKernelsTests
{
    /// <summary>The mode byte of 64 bit float weights.</summary>
    private const byte DoubleMode = 128;

    /// <summary>The seed of the generator of the test values.</summary>
    private const int Seed = 12_345;

    /// <summary>The largest difference from the plain loop; the two sum in different orders.</summary>
    private const float Tolerance = 1e-4F;

    /// <summary>The number of steps in a unit of the values the generator makes.</summary>
    private const int Resolution = 1000;

    /// <summary>The number of inputs of the matrices in the first test.</summary>
    private const int WholeInputs = 21;

    /// <summary>The number of inputs of the matrix in the partial test.</summary>
    private const int PartInputs = 17;

    /// <summary>The first input multiplied in the partial test.</summary>
    private const int PartFirst = 6;

    /// <summary>The number of outputs of the matrix in the partial test.</summary>
    private const int PartOutputs = 24;

    /// <summary>The number of inputs of the matrices in the join test.</summary>
    private const int JoinInputs = 9;

    /// <summary>The number of outputs of the first matrix in the join test.</summary>
    private const int JoinFirstOutputs = 5;

    /// <summary>The number of outputs of the second matrix in the join test.</summary>
    private const int JoinSecondOutputs = 11;

    /// <summary>The numbers of outputs tried: below one vector, a vector and a bit, several tiles, and a whole row of tiles.</summary>
    private static readonly int[] OutputCounts = [1, 5, 8, 13, 16, 40, 70, 111];

    /// <summary>The numbers of steps tried: one, fewer than a tile, a tile, and tiles with a few left over.</summary>
    private static readonly int[] StepCounts = [1, 2, 3, 4, 5, 8, 9];

    /// <summary>Every shape gives what a plain loop gives, with and without accumulating, and for part of the inputs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesAPlainLoop()
    {
        var random = new SeededRandom(Seed);
        foreach (var outputs in OutputCounts)
        {
            var matrix = CreateMatrix(random, outputs, WholeInputs, out var rows);
            foreach (var steps in StepCounts)
            {
                var input = RandomValues(random, steps * WholeInputs);
                var expected = Plain(rows, outputs, WholeInputs, input, steps, 0, WholeInputs);
                var actual = new float[steps * matrix.PaddedOutputs];
                matrix.FillBias(actual, matrix.PaddedOutputs, steps);
                matrix.Multiply(input, WholeInputs, actual, matrix.PaddedOutputs, new(steps, 0, WholeInputs, true));

                await AssertClose(expected, actual, matrix.PaddedOutputs, outputs, steps);
            }
        }
    }

    /// <summary>Multiplying only the last inputs adds their products to what the outputs hold.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipliesPartOfTheInputs()
    {
        var random = new SeededRandom(Seed);
        var matrix = CreateMatrix(random, PartOutputs, PartInputs, out var rows);
        var input = RandomValues(random, PartInputs - PartFirst);
        var start = RandomValues(random, matrix.PaddedOutputs);
        var actual = (float[])start.Clone();

        matrix.Multiply(input, PartInputs - PartFirst, actual, matrix.PaddedOutputs, new(1, PartFirst, PartInputs - PartFirst, true));

        var expected = Plain(rows, PartOutputs, PartInputs, input, 1, PartFirst, PartInputs - PartFirst);
        for (var i = 0; i < PartOutputs; i++)
        {
            var bias = rows[(i * (PartInputs + 1)) + PartInputs];
            await Assert.That(Math.Abs(actual[i] - start[i] - (expected[i] - bias))).IsLessThan(Tolerance);
        }
    }

    /// <summary>Joined matrices give their outputs one after another.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JoinedMatricesGiveTheirOutputsInOrder()
    {
        var random = new SeededRandom(Seed);
        var first = CreateMatrix(random, JoinFirstOutputs, JoinInputs, out var firstRows);
        var second = CreateMatrix(random, JoinSecondOutputs, JoinInputs, out var secondRows);
        var joined = WeightMatrix.Join([first, second]);
        var input = RandomValues(random, JoinInputs);
        var output = new float[joined.Outputs];

        joined.MultiplyAdd(input, output);

        var expected = Plain(firstRows, JoinFirstOutputs, JoinInputs, input, 1, 0, JoinInputs)
            .Concat(Plain(secondRows, JoinSecondOutputs, JoinInputs, input, 1, 0, JoinInputs))
            .ToArray();
        await Assert.That(joined.Outputs).IsEqualTo(first.Outputs + second.Outputs);
        for (var i = 0; i < output.Length; i++)
        {
            await Assert.That(Math.Abs(output[i] - expected[i])).IsLessThan(Tolerance);
        }
    }

    /// <summary>Makes values from -1 to 1.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="count">The number of values.</param>
    /// <returns>The values.</returns>
    private static float[] RandomValues(SeededRandom random, int count)
    {
        var values = new float[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (random.Next(-Resolution, Resolution + 1)) / (float)Resolution;
        }

        return values;
    }

    /// <summary>Makes a matrix of random weights from the stored form.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="inputs">The number of inputs.</param>
    /// <param name="rows">Receives the weights a row for each output, each ending in its bias.</param>
    /// <returns>The matrix.</returns>
    private static WeightMatrix CreateMatrix(SeededRandom random, int outputs, int inputs, out float[] rows)
    {
        rows = RandomValues(random, outputs * (inputs + 1));
        var builder = new ModelBytes().Byte(DoubleMode).Int(outputs).Int(inputs + 1).Double(0);
        foreach (var weight in rows)
        {
            _ = builder.Double(weight);
        }

        var reader = new ModelReader(builder.ToArray());
        return WeightMatrix.Read(ref reader);
    }

    /// <summary>Works out the products with a plain loop.</summary>
    /// <param name="rows">The weights a row for each output, each ending in its bias.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="inputs">The number of inputs.</param>
    /// <param name="input">The input steps, <paramref name="count"/> long each.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="first">The first input multiplied.</param>
    /// <param name="count">The number of inputs multiplied.</param>
    /// <returns>The outputs, step after step, with the bias added.</returns>
    private static float[] Plain(float[] rows, int outputs, int inputs, float[] input, int steps, int first, int count)
    {
        var result = new float[steps * outputs];
        for (var step = 0; step < steps; step++)
        {
            for (var output = 0; output < outputs; output++)
            {
                var sum = rows[(output * (inputs + 1)) + inputs];
                for (var k = first; k < first + count; k++)
                {
                    sum += rows[(output * (inputs + 1)) + k] * input[(step * count) + k - first];
                }

                result[(step * outputs) + output] = sum;
            }
        }

        return result;
    }

    /// <summary>Checks the outputs against the plain loop.</summary>
    /// <param name="expected">The plain loop's outputs.</param>
    /// <param name="actual">The padded outputs of the kernel.</param>
    /// <param name="stride">The distance between steps of <paramref name="actual"/>.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="steps">The number of steps.</param>
    /// <returns>A task.</returns>
    private static async Task AssertClose(float[] expected, float[] actual, int stride, int outputs, int steps)
    {
        for (var step = 0; step < steps; step++)
        {
            for (var output = 0; output < outputs; output++)
            {
                await Assert.That(Math.Abs(actual[(step * stride) + output] - expected[(step * outputs) + output])).IsLessThan(Tolerance);
            }
        }
    }
}
