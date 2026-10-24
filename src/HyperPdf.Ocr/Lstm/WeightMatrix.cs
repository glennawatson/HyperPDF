// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// The weights of one layer: a weight from every input to every output, and a bias for each output. They are stored a row
/// for each input (the order <see cref="MatrixKernels"/> reads them in) and widened to 32 bit floats once, on load.
/// </summary>
[DebuggerDisplay("WeightMatrix: {Outputs} outputs, {Inputs} inputs")]
public sealed class WeightMatrix
{
    /// <summary>The mode bit saying the weights are stored as 8 bit integers with a scale for each row.</summary>
    private const byte IntegerFlag = 1;

    /// <summary>The mode bit saying the weights are 64 bit floats rather than the older 32 bit floats.</summary>
    private const byte DoubleFlag = 128;

    /// <summary>The scale an 8 bit weight is stored with.</summary>
    private const float IntegerRange = 127F;

    /// <summary>The most rows or columns a matrix may have.</summary>
    private const uint MaxDimension = ushort.MaxValue;

    /// <summary>The weights, a row of <see cref="PaddedOutputs"/> for each input.</summary>
    private readonly float[] _weights;

    /// <summary>The bias of each output, <see cref="PaddedOutputs"/> long.</summary>
    private readonly float[] _bias;

    /// <summary>Initializes a new instance of the <see cref="WeightMatrix"/> class.</summary>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="inputs">The number of inputs.</param>
    /// <param name="weights">The weights, a row of <paramref name="paddedOutputs"/> for each input.</param>
    /// <param name="bias">The bias of each output, <paramref name="paddedOutputs"/> long.</param>
    /// <param name="paddedOutputs">The length of a row, a whole number of vectors.</param>
    private WeightMatrix(int outputs, int inputs, float[] weights, float[] bias, int paddedOutputs)
    {
        Outputs = outputs;
        Inputs = inputs;
        _weights = weights;
        _bias = bias;
        PaddedOutputs = paddedOutputs;
    }

    /// <summary>Gets the number of outputs.</summary>
    public int Outputs { get; }

    /// <summary>Gets the number of inputs.</summary>
    public int Inputs { get; }

    /// <summary>Gets the length a step of outputs must have when it is multiplied: the outputs rounded up to whole vectors.</summary>
    public int PaddedOutputs { get; }

    /// <summary>Reads a matrix.</summary>
    /// <param name="reader">The reader, placed at the matrix's mode byte.</param>
    /// <returns>The matrix.</returns>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    public static WeightMatrix Read(ref ModelReader reader)
    {
        var mode = reader.ReadByte();
        var integer = (mode & IntegerFlag) != 0;
        var wide = (mode & DoubleFlag) != 0;
        if (integer)
        {
            return ReadIntegers(ref reader, wide);
        }

        var (rows, columns) = ReadSize(ref reader);
        var weights = new float[checked(rows * columns)];
        if (wide)
        {
            _ = reader.ReadDouble();
            reader.ReadDoublesAsSingles(weights);
        }
        else
        {
            _ = reader.ReadSingle();
            reader.ReadSingles(weights);
        }

        return FromRows(rows, columns, weights);
    }

    /// <summary>Joins matrices that take the same inputs into one that gives all their outputs, one matrix's after another's.</summary>
    /// <param name="matrices">The matrices.</param>
    /// <returns>The joined matrix.</returns>
    /// <exception cref="InvalidDataException">The matrices take different numbers of inputs.</exception>
    public static WeightMatrix Join(WeightMatrix[] matrices)
    {
        ArgumentNullException.ThrowIfNull(matrices);
        var inputs = matrices[0].Inputs;
        var outputs = 0;
        foreach (var matrix in matrices)
        {
            outputs += matrix.Outputs;
            if (matrix.Inputs != inputs)
            {
                throw new InvalidDataException("Matrices that are joined must take the same number of inputs.");
            }
        }

        var padded = PadOutputs(outputs);
        var weights = new float[inputs * padded];
        var bias = new float[padded];
        var offset = 0;
        foreach (var matrix in matrices)
        {
            for (var k = 0; k < inputs; k++)
            {
                matrix._weights.AsSpan(k * matrix.PaddedOutputs, matrix.Outputs).CopyTo(weights.AsSpan((k * padded) + offset, matrix.Outputs));
            }

            matrix._bias.AsSpan(0, matrix.Outputs).CopyTo(bias.AsSpan(offset, matrix.Outputs));
            offset += matrix.Outputs;
        }

        return new(outputs, inputs, weights, bias, padded);
    }

    /// <summary>Sets every step of outputs to the biases, ready for products to be added.</summary>
    /// <param name="output">The output steps.</param>
    /// <param name="outputStride">The distance between the starts of two steps, at least <see cref="PaddedOutputs"/>.</param>
    /// <param name="steps">The number of steps.</param>
    public void FillBias(Span<float> output, int outputStride, int steps)
    {
        for (var step = 0; step < steps; step++)
        {
            _bias.CopyTo(output.Slice(step * outputStride, PaddedOutputs));
        }
    }

    /// <summary>Multiplies a batch of input steps by some of the inputs' weights, replacing or adding to the outputs.</summary>
    /// <param name="input">The input steps.</param>
    /// <param name="inputStride">The distance between the starts of two input steps.</param>
    /// <param name="output">The output steps.</param>
    /// <param name="outputStride">The distance between the starts of two output steps, at least <see cref="PaddedOutputs"/>.</param>
    /// <param name="range">The steps and inputs to multiply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Multiply(ReadOnlySpan<float> input, int inputStride, Span<float> output, int outputStride, MultiplyRange range) =>
        MatrixKernels.Multiply(_weights, PaddedOutputs, input, inputStride, output, outputStride, range);

    /// <summary>Computes <c>v = W·u + bias</c> for one step.</summary>
    /// <param name="input">The inputs, <see cref="Inputs"/> long.</param>
    /// <param name="output">Receives the outputs, <see cref="Outputs"/> long.</param>
    public void MultiplyAdd(ReadOnlySpan<float> input, Span<float> output)
    {
        var padded = new float[PaddedOutputs];
        FillBias(padded, PaddedOutputs, 1);
        Multiply(input, Inputs, padded, PaddedOutputs, new(1, 0, Inputs, true));
        padded.AsSpan(0, Outputs).CopyTo(output);
    }

    /// <summary>Rounds a number of outputs up to a whole number of vectors.</summary>
    /// <param name="outputs">The number of outputs.</param>
    /// <returns>The padded length.</returns>
    private static int PadOutputs(int outputs)
    {
        var width = Vector<float>.Count;
        return (outputs + width - 1) / width * width;
    }

    /// <summary>Builds a matrix from weights stored a row for each output, each row ending in its bias.</summary>
    /// <param name="rows">The number of outputs.</param>
    /// <param name="columns">The length of a row: the inputs and the bias.</param>
    /// <param name="stored">The weights as stored.</param>
    /// <returns>The matrix.</returns>
    private static WeightMatrix FromRows(int rows, int columns, float[] stored)
    {
        var inputs = columns - 1;
        var padded = PadOutputs(rows);
        var weights = new float[inputs * padded];
        var bias = new float[padded];
        for (var row = 0; row < rows; row++)
        {
            for (var k = 0; k < inputs; k++)
            {
                weights[(k * padded) + row] = stored[(row * columns) + k];
            }

            bias[row] = stored[(row * columns) + inputs];
        }

        return new(rows, inputs, weights, bias, padded);
    }

    /// <summary>Reads the number of rows and columns of a stored array.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The size.</returns>
    /// <exception cref="InvalidDataException">The size is invalid or the data ends first.</exception>
    private static MatrixSize ReadSize(ref ModelReader reader)
    {
        var rows = reader.ReadInt32();
        var columns = reader.ReadInt32();
        if ((uint)rows > MaxDimension || (uint)columns > MaxDimension || columns == 0)
        {
            throw new InvalidDataException("The recognition model has a weight matrix of an invalid size.");
        }

        return new(rows, columns);
    }

    /// <summary>Reads a matrix stored as 8 bit integers and a scale for each row.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="scaled">Whether the scales are stored as the current format does, as 64 bit floats.</param>
    /// <returns>The matrix, widened to floats.</returns>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    private static WeightMatrix ReadIntegers(ref ModelReader reader, bool scaled)
    {
        var (rows, columns) = ReadSize(ref reader);
        _ = reader.ReadSByte();
        var integers = reader.ReadBytes(checked(rows * columns));
        var scaleCount = reader.ReadUInt32();
        if (scaleCount < rows)
        {
            throw new InvalidDataException("The recognition model has too few weight scales.");
        }

        var scales = new float[rows];
        for (var i = 0; i < scaleCount; i++)
        {
            var scale = scaled ? (float)reader.ReadDouble() : reader.ReadSingle() * IntegerRange;
            if (i < rows)
            {
                scales[i] = scale;
            }
        }

        var weights = new float[rows * columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = (row * columns) + column;
                weights[index] = (sbyte)integers[index] * scales[row];
            }
        }

        return FromRows(rows, columns, weights);
    }
}
