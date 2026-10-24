// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Runs layers on the same input and puts their features side by side in each step.</summary>
[DebuggerDisplay("ParallelLayer: {Name}, {_layers.Length} layers")]
public sealed class ParallelLayer : NetworkLayer
{
    /// <summary>The layers.</summary>
    private readonly NetworkLayer[] _layers;

    /// <summary>Initializes a new instance of the <see cref="ParallelLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="layers">The layers.</param>
    public ParallelLayer(string name, NetworkLayer[] layers)
        : base(name, layers.Length > 0 ? layers[0].Inputs : 0, SumOutputs(layers)) => _layers = layers;

    /// <summary>Gets the layers.</summary>
    public IReadOnlyList<NetworkLayer> Layers => _layers;

    /// <inheritdoc/>
    public override int XScale => _layers.Length > 0 ? _layers[0].XScale : 1;

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        FeatureMap? output = null;
        var offset = 0;
        foreach (var layer in _layers)
        {
            var result = layer.Forward(input, scratch);
            output ??= scratch.RentMap(result.Height, result.Width, Outputs);
            if (result.Height != output.Height || result.Width != output.Width)
            {
                throw new InvalidDataException("The layers of a parallel group give different sizes.");
            }

            for (var step = 0; step < result.Steps; step++)
            {
                result.Step(step).CopyTo(output.Step(step)[offset..]);
            }

            offset += result.Depth;
            if (!ReferenceEquals(result, input))
            {
                scratch.Return(result);
            }
        }

        return output ?? input;
    }

    /// <summary>Adds up the features the layers give.</summary>
    /// <param name="layers">The layers.</param>
    /// <returns>The total.</returns>
    private static int SumOutputs(NetworkLayer[] layers)
    {
        var total = 0;
        foreach (var layer in layers)
        {
            total += layer.Outputs;
        }

        return total;
    }
}
