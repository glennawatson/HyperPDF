// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Why flattening left an annotation as it was, or removed it without drawing it.</summary>
public enum PdfFlattenSkipReason
{
    /// <summary>The annotation's Hidden flag is set, so it is neither shown nor printed.</summary>
    Hidden = 0,

    /// <summary>The annotation's NoView flag is set, so it is not shown on screen.</summary>
    NotShown = 1,

    /// <summary>The annotation's Print flag is not set, so it is not printed.</summary>
    NotPrinted = 2,

    /// <summary>The annotation has no appearance and none could be drawn for it.</summary>
    NoAppearance = 3,

    /// <summary>The annotation's appearance has no usable box, or the annotation has no rectangle.</summary>
    UnusableAppearance = 4,

    /// <summary>The annotation is in an optional content group and the document claims PDF/A-1, which forbids optional content.</summary>
    LayerNotAllowed = 5,

    /// <summary>The annotation is a trap network or a printer's mark, which belong to printing production and are kept.</summary>
    ProductionMark = 6,

    /// <summary>The annotation marks content for redaction that has not been applied; drawing it would not apply the redaction.</summary>
    RedactionNotApplied = 7,

    /// <summary>The annotation has no appearance, and drawing one would need a font that PDF/A requires to be embedded.</summary>
    NeedsFont = 8,
}
