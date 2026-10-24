// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// A recurrent layer that reads each row of steps from left to right, keeping a cell state that the cell input, input
/// gate, forget gate and output gate shape at every step. The state is cleared at the start of each row. The rows do not
/// depend on each other, so a step of every row is worked out together, and the part of every gate that depends only on
/// the layer's input is worked out for all the steps in one pass.
/// </summary>
[DebuggerDisplay("LstmLayer: {Name}, {Inputs} in, {Outputs} cells, summary={_summary}")]
public sealed class LstmLayer : NetworkLayer
{
    /// <summary>The number of gates in a layer that looks one way along a row.</summary>
    private const int GateCount = 4;

    /// <summary>The place of the cell input among the gates.</summary>
    private const int CellInputGate = 0;

    /// <summary>The place of the input gate among the gates.</summary>
    private const int InputGate = 1;

    /// <summary>The place of the forget gate among the gates.</summary>
    private const int ForgetGate = 2;

    /// <summary>The place of the output gate among the gates.</summary>
    private const int OutputGate = 3;

    /// <summary>The limit the cell state is held within.</summary>
    private const float StateClip = 100F;

    /// <summary>The weights of the four gates joined: the cell input's outputs, then the input gate's, the forget gate's and the output gate's.</summary>
    private readonly WeightMatrix _gates;

    /// <summary>Whether only the last step of each row is given.</summary>
    private readonly bool _summary;

    /// <summary>Initializes a new instance of the <see cref="LstmLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="inputs">The features in each input step.</param>
    /// <param name="outputs">The features in each output step, which is the size of the cell state.</param>
    /// <param name="gates">The four gate weights in the order cell input, input gate, forget gate, output gate.</param>
    /// <param name="summary">Whether only the last step of each row is given.</param>
    /// <exception cref="InvalidDataException">The weights do not fit the sizes.</exception>
    public LstmLayer(string name, int inputs, int outputs, WeightMatrix[] gates, bool summary)
        : base(name, inputs, outputs)
    {
        ArgumentNullException.ThrowIfNull(gates);
        if (gates.Length != GateCount)
        {
            throw new InvalidDataException("A recurrent layer needs four gates.");
        }

        foreach (var gate in gates)
        {
            if (gate.Outputs != outputs || gate.Inputs != inputs + outputs)
            {
                throw new InvalidDataException("A recurrent layer's weights do not match its size.");
            }
        }

        _gates = WeightMatrix.Join(gates);
        _summary = summary;
    }

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var rows = input.Height;
        var columns = input.Width;
        var size = Outputs;
        var output = scratch.RentMap(rows, _summary ? 1 : columns, size);
        if (rows == 0 || columns == 0)
        {
            return output;
        }

        var stride = _gates.PaddedOutputs;
        var projected = scratch.RentFloats(rows * columns * stride);
        _gates.FillBias(projected, stride, rows * columns);
        _gates.Multiply(input.Values, Inputs, projected, stride, new(rows * columns, 0, Inputs, true));
        var state = scratch.RentZeroedFloats(rows * size);
        var current = scratch.RentZeroedFloats(rows * size);
        var gates = scratch.RentFloats(rows * stride);
        for (var x = 0; x < columns; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                projected.AsSpan(((y * columns) + x) * stride, stride).CopyTo(gates.AsSpan(y * stride, stride));
            }

            _gates.Multiply(current, size, gates, stride, new(rows, Inputs, size, true));
            for (var y = 0; y < rows; y++)
            {
                Advance(state.AsSpan(y * size, size), gates.AsSpan(y * stride, stride), current.AsSpan(y * size, size));
                if (!_summary)
                {
                    current.AsSpan(y * size, size).CopyTo(output.At(y, x));
                }
            }
        }

        if (_summary)
        {
            for (var y = 0; y < rows; y++)
            {
                current.AsSpan(y * size, size).CopyTo(output.At(y, 0));
            }
        }

        scratch.Return(projected);
        scratch.Return(state);
        scratch.Return(current);
        scratch.Return(gates);
        return output;
    }

    /// <summary>Moves the cell state and output on by one step.</summary>
    /// <param name="state">The cell state, updated in place.</param>
    /// <param name="gates">The four gates' sums, one after another.</param>
    /// <param name="output">Receives the output.</param>
    private static void Advance(Span<float> state, ReadOnlySpan<float> gates, Span<float> output)
    {
        var size = state.Length;
        var width = Vector<float>.Count;
        var done = 0;
        if (size >= width)
        {
            var count = size / width;
            var cells = MemoryMarshal.Cast<float, Vector<float>>(state[..(count * width)]);
            var results = MemoryMarshal.Cast<float, Vector<float>>(output[..(count * width)]);
            var cellInput = MemoryMarshal.Cast<float, Vector<float>>(gates.Slice(CellInputGate * size, count * width));
            var inputGate = MemoryMarshal.Cast<float, Vector<float>>(gates.Slice(InputGate * size, count * width));
            var forget = MemoryMarshal.Cast<float, Vector<float>>(gates.Slice(ForgetGate * size, count * width));
            var show = MemoryMarshal.Cast<float, Vector<float>>(gates.Slice(OutputGate * size, count * width));
            var limit = new Vector<float>(StateClip);
            for (var v = 0; v < count; v++)
            {
                var next = (cells[v] * Activations.Logistic(forget[v])) + (Activations.Tanh(cellInput[v]) * Activations.Logistic(inputGate[v]));
                cells[v] = Vector.Min(Vector.Max(next, -limit), limit);
                results[v] = Activations.Tanh(cells[v]) * Activations.Logistic(show[v]);
            }

            done = count * width;
        }

        for (var i = done; i < size; i++)
        {
            var next = (state[i] * Activations.Logistic(gates[(ForgetGate * size) + i])) + (Activations.Tanh(gates[(CellInputGate * size) + i]) * Activations.Logistic(gates[(InputGate * size) + i]));
            state[i] = Math.Clamp(next, -StateClip, StateClip);
            output[i] = Activations.Tanh(state[i]) * Activations.Logistic(gates[(OutputGate * size) + i]);
        }
    }
}
