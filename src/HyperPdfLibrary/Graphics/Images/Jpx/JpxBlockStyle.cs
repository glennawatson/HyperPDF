// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The code-block style bits of the COD and COC markers (ISO 15444-1 table A.19).</summary>
[Flags]
internal enum JpxBlockStyle
{
    /// <summary>No mode switches.</summary>
    None = 0,

    /// <summary>Selective arithmetic coding bypass: later significance and refinement passes are raw bits.</summary>
    Bypass = 1 << 0,

    /// <summary>The context probabilities reset after each coding pass.</summary>
    Reset = 1 << 1,

    /// <summary>The arithmetic coder terminates after each coding pass.</summary>
    TerminateAll = 1 << 2,

    /// <summary>Contexts do not look at the stripe below.</summary>
    VerticallyCausal = 1 << 3,

    /// <summary>Predictable termination, which needs no decoder change.</summary>
    PredictableTermination = 1 << 4,

    /// <summary>A segmentation symbol follows each cleanup pass.</summary>
    SegmentationSymbols = 1 << 5,

    /// <summary>High-throughput block coding (T.814 | ISO 15444-15); the mode switches above then do not apply, except vertically causal.</summary>
    HighThroughput = 1 << 6,

    /// <summary>With <see cref="HighThroughput"/>, each code-block may be HT or regular (the MIXED mode, T.814 clause A.4).</summary>
    HighThroughputMixed = 1 << 7,
}
