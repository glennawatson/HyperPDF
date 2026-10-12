// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Reading;

/// <summary>What a block of text on a page is.</summary>
public enum ReadingBlockKind
{
    /// <summary>Ordinary running text.</summary>
    Paragraph = 0,

    /// <summary>A heading: larger or bold, and short.</summary>
    Heading = 1,

    /// <summary>One item of a bulleted or numbered list.</summary>
    ListItem = 2,

    /// <summary>A figure or table caption, such as "Figure 2: Results".</summary>
    Caption = 3,

    /// <summary>A footnote, read after the page's main text.</summary>
    Footnote = 4,

    /// <summary>A cell of a table, from a tagged document.</summary>
    TableCell = 5,

    /// <summary>A figure or formula, read by its alternative text.</summary>
    Figure = 6,
}
