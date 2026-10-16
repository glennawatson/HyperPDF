// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The kind of visual item inside a table cell.</summary>
internal enum CellItemKind
{
    /// <summary>A line of plain or styled text.</summary>
    Text = 0,

    /// <summary>An inline image.</summary>
    Image = 1,

    /// <summary>A line of a preformatted code block.</summary>
    Code = 2,

    /// <summary>A horizontal rule divider.</summary>
    Rule = 3,
}
