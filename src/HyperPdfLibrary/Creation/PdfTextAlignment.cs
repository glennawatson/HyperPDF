// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How lines of text sit between the left and right edges of the space they are drawn in.</summary>
public enum PdfTextAlignment
{
    /// <summary>Lines start at the left edge.</summary>
    Left = 0,

    /// <summary>Lines are centred.</summary>
    Center = 1,

    /// <summary>Lines end at the right edge.</summary>
    Right = 2,

    /// <summary>Lines fill the width, with the extra space shared between words; the last line of a paragraph starts at the left.</summary>
    Justify = 3,
}
