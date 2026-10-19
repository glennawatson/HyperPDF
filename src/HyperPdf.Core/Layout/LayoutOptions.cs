// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>Inputs to <see cref="DocumentLayout.Create"/>.</summary>
/// <param name="Rotation">The page rotation.</param>
/// <param name="Mode">The page arrangement.</param>
/// <param name="Scale">Device independent pixels per point.</param>
/// <param name="Spacing">The gap between pages.</param>
/// <param name="Margin">The margin around the content.</param>
/// <param name="ViewportWidth">The viewport width, used to centre narrow content.</param>
[DebuggerDisplay("LayoutOptions: {Mode} x{Scale}")]
public readonly record struct LayoutOptions(PageRotation Rotation, PageLayoutMode Mode, double Scale, double Spacing, double Margin, double ViewportWidth)
{
    /// <summary>
    /// Gets a value indicating whether pages are shown one row at a time: each row gets a slot at least
    /// <see cref="ViewportHeight"/> tall with the row centred in it, so a viewport aligned to a slot shows only that row.
    /// </summary>
    public bool PageByPage { get; init; }

    /// <summary>Gets a value indicating whether a two page row reads right to left: the first page of the row sits on the right and a cover on the left.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>Gets the viewport height, used for page by page slots.</summary>
    public double ViewportHeight { get; init; }
}
