// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>The settings of <see cref="Jbig2Encoder"/>.</summary>
[DebuggerDisplay("Jbig2EncodeOptions: {Mode}, template {GenericTemplate}")]
public sealed record Jbig2EncodeOptions
{
    /// <summary>Gets the default options: the smallest lossless coding, generic template 0 with typical prediction, and no separate globals.</summary>
    public static Jbig2EncodeOptions Default { get; } = new();

    /// <summary>Gets how the page is coded.</summary>
    public Jbig2EncodeMode Mode { get; init; } = Jbig2EncodeMode.Smallest;

    /// <summary>Gets the generic region template, 0 to 3 (T.88 section 6.2.5.3). Template 0 compresses best; 3 is the fastest.</summary>
    public int GenericTemplate { get; init; }

    /// <summary>Gets a value indicating whether typical prediction (TPGDON) is on for generic regions, which makes repeated rows nearly free.</summary>
    public bool TypicalPrediction { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the symbol dictionary goes in a separate globals stream (for /JBIG2Globals) instead of
    /// the image stream. Only the symbol modes have a dictionary to move.
    /// </summary>
    public bool SeparateGlobals { get; init; }

    /// <summary>
    /// Gets how alike two components must be, from 0.5 to 1, for <see cref="Jbig2EncodeMode.LossySymbolMerging"/> to share a symbol:
    /// the share of their combined black pixels that both have. Two components must also have every black pixel within one pixel
    /// of the other's. Higher is safer; 1 merges only identical shapes.
    /// </summary>
    public double MergeSimilarity { get; init; } = 0.90;

    /// <summary>Gets the largest width or height, in pixels, of a component that becomes a symbol; larger components are coded in a generic region.</summary>
    public int MaximumSymbolSize { get; init; } = 160;
}
