// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>Marks drawn on or behind a character, found from the lines and boxes the page draws near it.</summary>
[Flags]
public enum PdfTextDecorations
{
    /// <summary>No marks.</summary>
    None = 0,

    /// <summary>A thin line under the baseline that runs along the character.</summary>
    Underline = 1 << 0,

    /// <summary>A thin line through the middle of the character.</summary>
    Strikeout = 1 << 1,

    /// <summary>A coloured box behind the character, as a highlighter leaves.</summary>
    Highlight = 1 << 2,
}
