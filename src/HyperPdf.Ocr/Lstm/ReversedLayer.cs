// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Runs a layer on the picture mirrored or with its axes swapped, then puts the result back the right way round.</summary>
[DebuggerDisplay("ReversedLayer: {Name}, {_kind}")]
public sealed class ReversedLayer : NetworkLayer
{
    /// <summary>What is done to the picture before and after the inner layer.</summary>
    private readonly NetworkType _kind;

    /// <summary>The layer that runs on the changed picture.</summary>
    private readonly NetworkLayer _inner;

    /// <summary>Initializes a new instance of the <see cref="ReversedLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="kind"><see cref="NetworkType.XReversed"/>, <see cref="NetworkType.YReversed"/> or <see cref="NetworkType.XYTranspose"/>.</param>
    /// <param name="inner">The layer that runs on the changed picture.</param>
    public ReversedLayer(string name, NetworkType kind, NetworkLayer inner)
        : base(name, inner.Inputs, inner.Outputs)
    {
        _kind = kind;
        _inner = inner;
    }

    /// <inheritdoc/>
    public override int XScale => _inner.XScale;

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch)
    {
        var changed = Reorder(input, scratch);
        var result = _inner.Forward(changed, scratch);
        var restored = Reorder(result, scratch);
        scratch.Return(changed);
        if (!ReferenceEquals(result, changed))
        {
            scratch.Return(result);
        }

        return restored;
    }

    /// <summary>Mirrors a picture.</summary>
    /// <param name="source">The picture.</param>
    /// <param name="horizontally">Whether to mirror left to right rather than top to bottom.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The mirrored picture.</returns>
    private static FeatureMap Mirror(FeatureMap source, bool horizontally, RecognitionScratch scratch)
    {
        var result = scratch.RentMap(source.Height, source.Width, source.Depth);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                source.At(horizontally ? y : source.Height - 1 - y, horizontally ? source.Width - 1 - x : x).CopyTo(result.At(y, x));
            }
        }

        return result;
    }

    /// <summary>Swaps the rows and columns of a picture.</summary>
    /// <param name="source">The picture.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The transposed picture.</returns>
    private static FeatureMap Transpose(FeatureMap source, RecognitionScratch scratch)
    {
        var result = scratch.RentMap(source.Width, source.Height, source.Depth);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                source.At(y, x).CopyTo(result.At(x, y));
            }
        }

        return result;
    }

    /// <summary>Mirrors or transposes a picture. Doing it twice gives the original.</summary>
    /// <param name="source">The picture.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The changed picture.</returns>
    private FeatureMap Reorder(FeatureMap source, RecognitionScratch scratch) => _kind switch
    {
        NetworkType.XReversed => Mirror(source, true, scratch),
        NetworkType.YReversed => Mirror(source, false, scratch),
        _ => Transpose(source, scratch),
    };
}
