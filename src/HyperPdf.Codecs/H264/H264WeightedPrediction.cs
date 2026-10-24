// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Applies explicit weights to a predicted block (clause 8.4.2.3 of ITU-T H.264, for one reference list).</summary>
internal static class H264WeightedPrediction
{
    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>Weights the predicted samples of a block in place.</summary>
    /// <param name="plane">The plane that holds the prediction.</param>
    /// <param name="stride">The samples in a row of the plane.</param>
    /// <param name="block">The block's position and size.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="offset">The offset added after weighting.</param>
    /// <param name="log2Denom">The log2 of the weight's denominator.</param>
    internal static void Apply(byte[] plane, int stride, BlockRect block, int weight, int offset, int log2Denom)
    {
        if (weight == 1 << log2Denom && offset == 0)
        {
            return;
        }

        var rounding = log2Denom >= 1 ? 1 << (log2Denom - 1) : 0;
        for (var row = 0; row < block.Height; row++)
        {
            var index = ((block.Y + row) * stride) + block.X;
            for (var column = 0; column < block.Width; column++)
            {
                var weighted = (((plane[index + column] * weight) + rounding) >> log2Denom) + offset;
                plane[index + column] = (byte)Math.Clamp(weighted, 0, MaxSample);
            }
        }
    }
}
