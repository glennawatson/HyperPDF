// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>One line of a paragraph: which atoms it holds and where they sit.</summary>
[DebuggerDisplay("LineBox: atoms {Start}..{End}, {Height} pt")]
internal sealed class LineBox
{
    /// <summary>Gets or sets the first atom of the line.</summary>
    internal int Start { get; set; }

    /// <summary>Gets or sets the place after the last atom of the line.</summary>
    internal int End { get; set; }

    /// <summary>Gets or sets the left edge of the space the line fills, from the left edge of the text area.</summary>
    internal float AvailableLeft { get; set; }

    /// <summary>Gets or sets the width of the space the line fills.</summary>
    internal float AvailableWidth { get; set; }

    /// <summary>Gets or sets the width of the line's content, without trailing spaces.</summary>
    internal float ContentWidth { get; set; }

    /// <summary>Gets or sets how far the tallest text rises above the baseline.</summary>
    internal float Ascent { get; set; }

    /// <summary>Gets or sets how far the lowest text falls below the baseline.</summary>
    internal float Descent { get; set; }

    /// <summary>Gets or sets the height of the line after line spacing.</summary>
    internal float Height { get; set; }

    /// <summary>Gets or sets the distance from the top of the line to its baseline.</summary>
    internal float Baseline { get; set; }

    /// <summary>Gets or sets a value indicating whether the line ends at a line break.</summary>
    internal bool EndsWithBreak { get; set; }

    /// <summary>Gets or sets a value indicating whether the line is the last of its paragraph.</summary>
    internal bool IsLast { get; set; }

    /// <summary>Gets or sets the extra width in points added to each space to fill the line, for justified text.</summary>
    internal float SpaceExtra { get; set; }

    /// <summary>Gets the widths of the tabs on the line.</summary>
    internal List<TabGap> Tabs { get; } = [];

    /// <summary>Gets the places in the paragraph's notes of the footnotes whose numbers are on the line.</summary>
    internal List<int> Notes { get; } = [];
}
