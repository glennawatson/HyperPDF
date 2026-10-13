// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>When shown text is wrapped in <c>/ActualText</c> marked content (ISO 32000-2, 14.9.4).</summary>
public enum PdfActualTextPolicy
{
    /// <summary>Never; a glyph's text is only what <c>/ToUnicode</c> says, so reordered or split clusters extract wrongly.</summary>
    Never = 0,

    /// <summary>
    /// When a glyph's text cannot be given by <c>/ToUnicode</c> alone: a cluster of several glyphs, as a reordered Indic
    /// syllable or a base with marks is, or a glyph used for different text.
    /// </summary>
    WhenNeeded = 1,

    /// <summary>As <see cref="WhenNeeded"/>, and also for ligatures, where one glyph shows several characters.</summary>
    Ligatures = 2,
}
