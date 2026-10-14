// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The code-block mode switches a JPEG 2000 encoder may use (ISO 15444-1 table A.19). They add error resilience and raise the size.</summary>
[Flags]
public enum PdfJpxBlockStyle
{
    /// <summary>No mode switches.</summary>
    None = 0,

    /// <summary>Selective arithmetic coding bypass: later significance and refinement passes are written as raw bits.</summary>
    Bypass = 1 << 0,

    /// <summary>The context probabilities reset after each coding pass.</summary>
    Reset = 1 << 1,

    /// <summary>The arithmetic coder terminates after each coding pass.</summary>
    TerminateAll = 1 << 2,

    /// <summary>Contexts do not look at the stripe below.</summary>
    VerticallyCausal = 1 << 3,

    /// <summary>A segmentation symbol follows each cleanup pass.</summary>
    SegmentationSymbols = 1 << 5,
}
