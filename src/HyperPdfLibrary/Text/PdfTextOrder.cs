// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>The order extraction reads a page's text in.</summary>
public enum PdfTextOrder
{
    /// <summary>
    /// The order the page draws its text in, which is the reading order of most documents. Runs that share a baseline are
    /// one line, in left-to-right order; right-to-left text is put in logical order; a glyph drawn twice is kept once.
    /// </summary>
    Content = 0,

    /// <summary>
    /// The order the text sits on the page: lines from top to bottom, and the runs on one line from left to right, so
    /// columns read across as in a layout-preserving export. A page with rotated or vertical text keeps its content order.
    /// </summary>
    Layout = 1,

    /// <summary>
    /// The order the glyphs are drawn in, with nothing sorted, merged or reordered: no run is dropped for repeating another
    /// and right-to-left text is left in drawing order. This is what a script that only wants the page's own sequence needs.
    /// </summary>
    Raw = 2,
}
