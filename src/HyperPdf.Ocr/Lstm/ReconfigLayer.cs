// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Shrinks the picture by stacking the features of each block of steps into one step.</summary>
[DebuggerDisplay("ReconfigLayer: {Name}, blocks {_blockWidth} by {_blockHeight}")]
public sealed class ReconfigLayer : NetworkLayer
{
    /// <summary>The block width.</summary>
    private readonly int _blockWidth;

    /// <summary>The block height.</summary>
    private readonly int _blockHeight;

    /// <summary>Initializes a new instance of the <see cref="ReconfigLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="inputs">The features in each step.</param>
    /// <param name="blockWidth">The block width.</param>
    /// <param name="blockHeight">The block height.</param>
    public ReconfigLayer(string name, int inputs, int blockWidth, int blockHeight)
        : base(name, inputs, inputs * blockWidth * blockHeight)
    {
        _blockWidth = blockWidth;
        _blockHeight = blockHeight;
    }

    /// <inheritdoc/>
    public override int XScale => _blockWidth;

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var output = scratch.RentMap(input.Height / _blockHeight, input.Width / _blockWidth, Outputs);
        for (var y = 0; y < output.Height; y++)
        {
            for (var x = 0; x < output.Width; x++)
            {
                var step = output.At(y, x);
                for (var dx = 0; dx < _blockWidth; dx++)
                {
                    for (var dy = 0; dy < _blockHeight; dy++)
                    {
                        var part = step.Slice(((dx * _blockHeight) + dy) * Inputs, Inputs);
                        input.At((y * _blockHeight) + dy, (x * _blockWidth) + dx).CopyTo(part);
                    }
                }
            }
        }

        return output;
    }
}
