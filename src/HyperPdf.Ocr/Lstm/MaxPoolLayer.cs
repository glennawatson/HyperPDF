// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Shrinks the picture by keeping, for each feature, the largest value in each block of steps.</summary>
[DebuggerDisplay("MaxPoolLayer: {Name}, blocks {_blockWidth} by {_blockHeight}")]
public sealed class MaxPoolLayer : NetworkLayer
{
    /// <summary>The block width.</summary>
    private readonly int _blockWidth;

    /// <summary>The block height.</summary>
    private readonly int _blockHeight;

    /// <summary>Initializes a new instance of the <see cref="MaxPoolLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="inputs">The features in each step.</param>
    /// <param name="blockWidth">The block width.</param>
    /// <param name="blockHeight">The block height.</param>
    public MaxPoolLayer(string name, int inputs, int blockWidth, int blockHeight)
        : base(name, inputs, inputs)
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
                input.At(y * _blockHeight, x * _blockWidth).CopyTo(step);
                for (var dx = 0; dx < _blockWidth; dx++)
                {
                    for (var dy = 0; dy < _blockHeight; dy++)
                    {
                        Max(step, input.At((y * _blockHeight) + dy, (x * _blockWidth) + dx));
                    }
                }
            }
        }

        return output;
    }

    /// <summary>Raises each value to at least the matching value of another run.</summary>
    /// <param name="values">The values to raise.</param>
    /// <param name="other">The values to compare with.</param>
    private static void Max(Span<float> values, ReadOnlySpan<float> other)
    {
        var width = Vector<float>.Count;
        var i = 0;
        if (values.Length >= width)
        {
            var left = MemoryMarshal.Cast<float, Vector<float>>(values);
            var right = MemoryMarshal.Cast<float, Vector<float>>(other);
            for (var v = 0; v < left.Length; v++)
            {
                left[v] = Vector.Max(left[v], right[v]);
            }

            i = left.Length * width;
        }

        for (; i < values.Length; i++)
        {
            values[i] = Math.Max(values[i], other[i]);
        }
    }
}
