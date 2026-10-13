// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The line style of a widget's border (<c>/BS /S</c>, ISO 32000-2 12.5.4).</summary>
public enum PdfFieldBorderStyle
{
    /// <summary>A solid line.</summary>
    Solid = 0,

    /// <summary>A dashed line.</summary>
    Dashed = 1,

    /// <summary>A raised border.</summary>
    Beveled = 2,

    /// <summary>A sunken border.</summary>
    Inset = 3,

    /// <summary>A line along the bottom edge only.</summary>
    Underline = 4,
}
