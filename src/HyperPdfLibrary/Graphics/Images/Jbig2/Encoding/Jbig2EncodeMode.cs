// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>How <see cref="Jbig2Encoder"/> codes a page.</summary>
public enum Jbig2EncodeMode
{
    /// <summary>Lossless: one arithmetic-coded generic region (T.88 section 6.2), the best choice for pictures and dithered scans.</summary>
    GenericRegion = 0,

    /// <summary>Lossless: one generic region coded with MMR, which is T.6 Group 4 fax coding.</summary>
    GenericRegionMmr = 1,

    /// <summary>
    /// Lossless: connected components become symbols in a symbol dictionary, a text region places them, and any component
    /// too large to be a symbol goes in a generic region. Identical shapes share one symbol; the page is reproduced bit for bit.
    /// </summary>
    SymbolText = 2,

    /// <summary>
    /// LOSSY: like <see cref="SymbolText"/>, but components that look alike are replaced by one shared symbol. The decoded
    /// page differs from the original, and a poor <see cref="Jbig2EncodeOptions.MergeSimilarity"/> can swap one character for
    /// another of similar shape. Use it only when that is acceptable.
    /// </summary>
    LossySymbolMerging = 3,

    /// <summary>Lossless: tries <see cref="GenericRegion"/>, <see cref="GenericRegionMmr"/>, <see cref="SymbolText"/> and <see cref="SymbolTextRefined"/> and keeps the smallest result.</summary>
    Smallest = 4,

    /// <summary>
    /// Lossless: like <see cref="SymbolText"/>, but a mark that only looks like an earlier symbol is coded as a refinement of
    /// it, so the page is reproduced bit for bit and similar marks cost far less than new symbols. A near copy that occurs
    /// more than once becomes a symbol of its own in a second dictionary that is itself coded as refinements of the first.
    /// </summary>
    SymbolTextRefined = 5,
}
