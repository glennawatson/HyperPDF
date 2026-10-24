// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>A layer that multiplies every step by a weight matrix, adds a bias, and squashes the result.</summary>
[DebuggerDisplay("FullyConnectedLayer: {Name}, {_kind}, {Inputs} in, {Outputs} out")]
public sealed class FullyConnectedLayer : NetworkLayer
{
    /// <summary>The kind of squashing.</summary>
    private readonly NetworkType _kind;

    /// <summary>The weights, with a bias for each output.</summary>
    private readonly WeightMatrix _weights;

    /// <summary>Initializes a new instance of the <see cref="FullyConnectedLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="kind">The kind of squashing, one of the layer types from <see cref="NetworkType.Logistic"/> to <see cref="NetworkType.SoftmaxNoCtc"/>.</param>
    /// <param name="weights">The weights.</param>
    /// <exception cref="InvalidDataException">The kind is not a fully connected one.</exception>
    public FullyConnectedLayer(string name, NetworkType kind, WeightMatrix weights)
        : base(name, weights.Inputs, weights.Outputs)
    {
        if (kind is < NetworkType.Logistic or > NetworkType.SoftmaxNoCtc)
        {
            throw new InvalidDataException($"{kind} is not a fully connected layer.");
        }

        _kind = kind;
        _weights = weights;
    }

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var output = scratch.RentMap(input.Height, input.Width, Outputs);
        var steps = input.Steps;
        if (steps == 0)
        {
            return output;
        }

        var padded = _weights.PaddedOutputs;
        if (padded == Outputs)
        {
            _weights.FillBias(output.Values, Outputs, steps);
            _weights.Multiply(input.Values, Inputs, output.Values, Outputs, new(steps, 0, Inputs, true));
        }
        else
        {
            var wide = scratch.RentFloats(steps * padded);
            _weights.FillBias(wide, padded, steps);
            _weights.Multiply(input.Values, Inputs, wide, padded, new(steps, 0, Inputs, true));
            for (var step = 0; step < steps; step++)
            {
                wide.AsSpan(step * padded, Outputs).CopyTo(output.Step(step));
            }

            scratch.Return(wide);
        }

        Squash(output);
        return output;
    }

    /// <summary>Applies the layer's squashing function to every step.</summary>
    /// <param name="output">The steps, changed in place.</param>
    private void Squash(FeatureMap output)
    {
        switch (_kind)
        {
            case NetworkType.Tanh:
            {
                Activations.Tanh(output.Span);
                break;
            }

            case NetworkType.Logistic:
            {
                Activations.Logistic(output.Span);
                break;
            }

            case NetworkType.PositiveClip:
            {
                Activations.ClipUnit(output.Span);
                break;
            }

            case NetworkType.SymmetricClip:
            {
                Activations.ClipSigned(output.Span);
                break;
            }

            case NetworkType.Relu:
            {
                Activations.Relu(output.Span);
                break;
            }

            case NetworkType.Softmax or NetworkType.SoftmaxNoCtc:
            {
                for (var step = 0; step < output.Steps; step++)
                {
                    Activations.Softmax(output.Step(step));
                }

                break;
            }

            default:
            {
                break;
            }
        }
    }
}
