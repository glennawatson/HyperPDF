// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The base direction of a line of text, which decides how runs of different directions are ordered (Unicode Standard Annex #9).</summary>
public enum PdfTextDirection
{
    /// <summary>The direction of the first strong letter, or left to right when there is none.</summary>
    Auto = 0,

    /// <summary>Left to right.</summary>
    LeftToRight = 1,

    /// <summary>Right to left.</summary>
    RightToLeft = 2,
}
