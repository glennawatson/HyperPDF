// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One line of the focus mode column: a page's name, one wrapped line of a paragraph, or the space between two paragraphs.</summary>
/// <param name="Kind">How the line is drawn.</param>
/// <param name="Block">The number of the paragraph the line belongs to, or -1 for a line that belongs to none.</param>
/// <param name="Page">The zero based page the line belongs to.</param>
/// <param name="Starts">Whether the line is the first of its paragraph, which is the one that can be pressed.</param>
/// <param name="Text">The line's text, empty for a space between paragraphs.</param>
[DebuggerDisplay("FocusLine: page {Page} '{Text}'")]
internal sealed record FocusLine(ChromeItemKind Kind, int Block, int Page, bool Starts, string Text)
{
    /// <summary>Gets where the line's text starts in its paragraph's text, so a word being read can be found in the line.</summary>
    internal int Start { get; init; }
}
