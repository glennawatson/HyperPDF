// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>What changes about how page objects are drawn besides their colours: the dashes, ends and corners of their lines, and how solid they are.</summary>
/// <param name="ChangeLine">Whether the dashes, ends and corners are changed.</param>
/// <param name="Dash">The pattern of dashes, when the line is changed.</param>
/// <param name="Cap">How the ends of the lines are drawn, when the line is changed.</param>
/// <param name="Join">How the corners of the lines are drawn, when the line is changed.</param>
/// <param name="ChangeOpacity">Whether how solid the objects are is changed.</param>
/// <param name="Opacity">How solid they are, from 0 for invisible to 1 for solid, when it is changed.</param>
[DebuggerDisplay("PageObjectLook: line {ChangeLine} {Dash}, opacity {ChangeOpacity} {Opacity}")]
public readonly record struct PageObjectLook(bool ChangeLine, PageLineDash Dash, PageLineCap Cap, PageLineJoin Join, bool ChangeOpacity, float Opacity)
{
    /// <summary>Makes a change of the line only.</summary>
    /// <param name="dash">The dashes.</param>
    /// <param name="cap">The ends.</param>
    /// <param name="join">The corners.</param>
    /// <returns>The change.</returns>
    public static PageObjectLook OfLine(PageLineDash dash, PageLineCap cap, PageLineJoin join) => new(true, dash, cap, join, false, 1F);

    /// <summary>Makes a change of how solid the objects are only.</summary>
    /// <param name="opacity">How solid, from 0 to 1.</param>
    /// <returns>The change.</returns>
    public static PageObjectLook OfOpacity(float opacity) => new(false, PageLineDash.Solid, PageLineCap.Butt, PageLineJoin.Miter, true, opacity);
}
