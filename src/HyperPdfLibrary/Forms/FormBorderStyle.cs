// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The border styles of a widget (<c>/BS /S</c>).</summary>
internal enum FormBorderStyle
{
    /// <summary>A solid line.</summary>
    Solid = 0,

    /// <summary>A dashed line.</summary>
    Dashed = 1,

    /// <summary>A line on the bottom edge only.</summary>
    Underline = 2,

    /// <summary>A raised border: light on the top and left, dark on the bottom and right.</summary>
    Beveled = 3,

    /// <summary>A sunken border: dark on the top and left, light on the bottom and right.</summary>
    Inset = 4,
}
