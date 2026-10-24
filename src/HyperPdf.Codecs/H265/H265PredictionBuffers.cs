// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The work areas of inter prediction: the 14 bit intermediate predictions of each list and the rows the separable interpolation passes between its two stages.</summary>
internal sealed class H265PredictionBuffers
{
    /// <summary>The largest prediction block side.</summary>
    private const int MaxSide = 64;

    /// <summary>The extra rows the vertical filter needs above and below a block.</summary>
    private const int FilterExtra = 7;

    /// <summary>Initializes a new instance of the <see cref="H265PredictionBuffers"/> class.</summary>
    internal H265PredictionBuffers()
    {
        List0 = new short[MaxSide * MaxSide];
        List1 = new short[MaxSide * MaxSide];
        Horizontal = new short[(MaxSide + FilterExtra) * MaxSide];
        Reference = new ushort[(MaxSide + FilterExtra) * (MaxSide + FilterExtra)];
    }

    /// <summary>Gets the block of reference samples and the margins the filter reads around it, row after row.</summary>
    internal ushort[] Reference { get; }

    /// <summary>Gets the list 0 prediction, row after row.</summary>
    internal short[] List0 { get; }

    /// <summary>Gets the list 1 prediction, row after row.</summary>
    internal short[] List1 { get; }

    /// <summary>Gets the rows filtered horizontally, before the vertical filter.</summary>
    internal short[] Horizontal { get; }
}
