// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Runs layers one after another, each taking the one before's output.</summary>
[DebuggerDisplay("SeriesLayer: {Name}, {_layers.Length} layers")]
public sealed class SeriesLayer : NetworkLayer
{
    /// <summary>The layers, in the order they run.</summary>
    private readonly NetworkLayer[] _layers;

    /// <summary>Initializes a new instance of the <see cref="SeriesLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="layers">The layers, in the order they run.</param>
    public SeriesLayer(string name, NetworkLayer[] layers)
        : base(name, layers.Length > 0 ? layers[0].Inputs : 0, layers.Length > 0 ? layers[^1].Outputs : 0) => _layers = layers;

    /// <summary>Gets the layers, in the order they run.</summary>
    public IReadOnlyList<NetworkLayer> Layers => _layers;

    /// <inheritdoc/>
    public override int XScale
    {
        get
        {
            var scale = 1;
            foreach (var layer in _layers)
            {
                scale *= layer.XScale;
            }

            return scale;
        }
    }

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var current = input;
        foreach (var layer in _layers)
        {
            var next = layer.Forward(current, scratch);
            if (!ReferenceEquals(current, input) && !ReferenceEquals(current, next))
            {
                scratch.Return(current);
            }

            current = next;
        }

        return current;
    }
}
