// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Layout;

/// <summary>The direction pages are laid out and scrolled in.</summary>
public enum ScrollMode
{
    /// <summary>Rows of pages follow one another down the document.</summary>
    Vertical = 0,

    /// <summary>All pages sit in one row, which scrolls from side to side.</summary>
    Horizontal = 1,

    /// <summary>Pages fill each row across the window and the next row starts below, so a wide window shows several pages and the document scrolls down.</summary>
    Wrapped = 2,
}
