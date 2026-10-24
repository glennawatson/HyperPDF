// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks that every stored form of a layer's weights reads and multiplies the same way.</summary>
public sealed class WeightMatrixTests
{
    /// <summary>The mode byte of 64 bit float weights.</summary>
    private const byte DoubleMode = 128;

    /// <summary>The mode byte of 8 bit integer weights with 64 bit scales.</summary>
    private const byte IntegerMode = 129;

    /// <summary>The mode byte of the older 32 bit float weights.</summary>
    private const byte SingleMode = 0;

    /// <summary>The mode byte of 64 bit float weights from a training run that used the Adam optimiser.</summary>
    private const byte AdamMode = 132;

    /// <summary>The number of outputs of the test matrix.</summary>
    private const int Outputs = 2;

    /// <summary>The number of columns of the test matrix: two inputs and a bias.</summary>
    private const int Columns = 3;

    /// <summary>The scale of the first row of integer weights.</summary>
    private const double FirstScale = 0.5;

    /// <summary>The scale of the second row of integer weights.</summary>
    private const double SecondScale = 2;

    /// <summary>The first output for the inputs one and one: 1 + 2 + 3.</summary>
    private const float FirstResult = 6F;

    /// <summary>The second output for the inputs one and one with 64 bit or 32 bit weights: 4 + 5 + 6.</summary>
    private const float SecondResult = 15F;

    /// <summary>The second output for the integer weights: 2 + 4 + 6.</summary>
    private const float IntegerSecondResult = 12F;

    /// <summary>The weights of the test matrix, row after row, each row ending in its bias.</summary>
    private static readonly double[] Rows = [1, 2, 3, 4, 5, 6];

    /// <summary>The integer weights, which the scales turn into the same first row and double the second.</summary>
    private static readonly sbyte[] IntegerRows = [2, 4, 6, 1, 2, 3];

    /// <summary>The inputs of every multiplication.</summary>
    private static readonly float[] Inputs = [1F, 1F];

    /// <summary>64 bit weights multiply and add the bias.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipliesDoubleWeights()
    {
        var matrix = Read(DoubleBytes(DoubleMode));

        await AssertResult(matrix, FirstResult, SecondResult);
    }

    /// <summary>The Adam flag only says how the model was trained, so the weights read the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IgnoresTheOptimiserFlag()
    {
        var matrix = Read(DoubleBytes(AdamMode));

        await AssertResult(matrix, FirstResult, SecondResult);
    }

    /// <summary>Integer weights are scaled by their row's scale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipliesIntegerWeights()
    {
        var builder = new ModelBytes().Byte(IntegerMode).Int(Outputs).Int(Columns).SByte(0);
        foreach (var weight in IntegerRows)
        {
            _ = builder.SByte(weight);
        }

        var matrix = Read(builder.UInt(Outputs).Double(FirstScale).Double(SecondScale).ToArray());

        await AssertResult(matrix, FirstResult, IntegerSecondResult);
    }

    /// <summary>The older 32 bit float form still reads.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipliesSingleWeights()
    {
        var builder = new ModelBytes().Byte(SingleMode).Int(Outputs).Int(Columns).Float(0F);
        foreach (var weight in Rows)
        {
            _ = builder.Float((float)weight);
        }

        await AssertResult(Read(builder.ToArray()), FirstResult, SecondResult);
    }

    /// <summary>A matrix with no columns is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesAMatrixWithNoColumns()
    {
        var bytes = new ModelBytes().Byte(DoubleMode).Int(Outputs).Int(0).Double(0).ToArray();

        await Assert.That(() => Read(bytes)).Throws<InvalidDataException>();
    }

    /// <summary>Makes the bytes of the test matrix as 64 bit floats.</summary>
    /// <param name="mode">The mode byte.</param>
    /// <returns>The bytes.</returns>
    private static byte[] DoubleBytes(byte mode)
    {
        var builder = new ModelBytes().Byte(mode).Int(Outputs).Int(Columns).Double(0);
        foreach (var weight in Rows)
        {
            _ = builder.Double(weight);
        }

        return builder.ToArray();
    }

    /// <summary>Reads a matrix from bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The matrix.</returns>
    private static WeightMatrix Read(byte[] bytes)
    {
        var reader = new ModelReader(bytes);
        return WeightMatrix.Read(ref reader);
    }

    /// <summary>Checks the sizes and the product with the test inputs.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="first">The expected first output.</param>
    /// <param name="second">The expected second output.</param>
    /// <returns>A task.</returns>
    private static async Task AssertResult(WeightMatrix matrix, float first, float second)
    {
        var output = new float[Outputs];

        matrix.MultiplyAdd(Inputs, output);

        await Assert.That(matrix.Outputs).IsEqualTo(Outputs);
        await Assert.That(matrix.Inputs).IsEqualTo(Columns - 1);
        await Assert.That(output[0]).IsEqualTo(first);
        await Assert.That(output[1]).IsEqualTo(second);
    }
}
