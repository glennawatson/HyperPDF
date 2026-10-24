// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// Joins each step with the steps around it: the features of a block of <c>(2 * halfX + 1)</c> by <c>(2 * halfY + 1)</c>
/// steps become one step. The part of a block beyond the picture's edge is filled with noise, as the model was trained.
/// </summary>
[DebuggerDisplay("ConvolveLayer: {Name}, reaches {_reachX} by {_reachY}")]
public sealed class ConvolveLayer : NetworkLayer
{
    /// <summary>The number of blocks of neighbours a reach covers: one each side of the centre, and the centre.</summary>
    private const int SidesAndCentre = 2;

    /// <summary>The distance to the neighbours to the left and right.</summary>
    private readonly int _reachX;

    /// <summary>The distance to the neighbours above and below.</summary>
    private readonly int _reachY;

    /// <summary>Initializes a new instance of the <see cref="ConvolveLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="inputs">The features in each input step.</param>
    /// <param name="reachX">The distance to the neighbours to the left and right.</param>
    /// <param name="reachY">The distance to the neighbours above and below.</param>
    public ConvolveLayer(string name, int inputs, int reachX, int reachY)
        : base(name, inputs, inputs * Extent(reachX) * Extent(reachY))
    {
        _reachX = reachX;
        _reachY = reachY;
    }

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var output = scratch.RentMap(input.Height, input.Width, Outputs);
        var group = Extent(_reachY) * Inputs;
        for (var y = 0; y < input.Height; y++)
        {
            var insideRows = y >= _reachY && y < input.Height - _reachY;
            for (var x = 0; x < input.Width; x++)
            {
                if (insideRows && x >= _reachX && x < input.Width - _reachX)
                {
                    CopyInterior(input, output, y, x);
                    continue;
                }

                var step = output.At(y, x);
                var offset = 0;
                for (var dx = -_reachX; dx <= _reachX; dx++)
                {
                    FillColumn(input, step.Slice(offset, group), y, x + dx, scratch.Random);
                    offset += group;
                }
            }
        }

        return output;
    }

    /// <summary>Gets the number of steps a reach covers along its axis.</summary>
    /// <param name="reach">The distance to the neighbours on each side.</param>
    /// <returns>The number of steps.</returns>
    private static int Extent(int reach) => (SidesAndCentre * reach) + 1;

    /// <summary>Fills values with noise.</summary>
    /// <param name="values">The values.</param>
    /// <param name="random">The generator.</param>
    private static void Randomize(Span<float> values, LehmerRandom random)
    {
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = random.NextSigned();
        }
    }

    /// <summary>Copies the block around a step that lies wholly inside the picture, with no checks for the edge.</summary>
    /// <param name="input">The input values.</param>
    /// <param name="output">The output values.</param>
    /// <param name="y">The row of the centre step.</param>
    /// <param name="x">The column of the centre step.</param>
    private void CopyInterior(FeatureMap input, FeatureMap output, int y, int x)
    {
        var source = input.Values;
        var destination = output.Values;
        var width = input.Width;
        var to = ((y * width) + x) * Outputs;
        for (var dx = -_reachX; dx <= _reachX; dx++)
        {
            for (var dy = -_reachY; dy <= _reachY; dy++)
            {
                var from = (((y + dy) * width) + x + dx) * Inputs;
                for (var feature = 0; feature < Inputs; feature++)
                {
                    destination[to + feature] = source[from + feature];
                }

                to += Inputs;
            }
        }
    }

    /// <summary>Fills the features of one column of the block.</summary>
    /// <param name="input">The input values.</param>
    /// <param name="destination">Receives the features of the column.</param>
    /// <param name="y">The row of the centre step.</param>
    /// <param name="x">The column.</param>
    /// <param name="random">The source of noise for steps beyond the picture.</param>
    private void FillColumn(FeatureMap input, Span<float> destination, int y, int x, LehmerRandom random)
    {
        if ((uint)x >= (uint)input.Width)
        {
            Randomize(destination, random);
            return;
        }

        for (var dy = -_reachY; dy <= _reachY; dy++)
        {
            var part = destination.Slice((dy + _reachY) * Inputs, Inputs);
            if ((uint)(y + dy) >= (uint)input.Height)
            {
                Randomize(part, random);
            }
            else
            {
                input.At(y + dy, x).CopyTo(part);
            }
        }
    }
}
