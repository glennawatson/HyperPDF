// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Where the pointer is on the page canvas: the page under it and the point on that page, found once per move.</summary>
/// <param name="Page">The page under the pointer, or -1 when it is over no page.</param>
/// <param name="Point">The point in page space; meaningless when <paramref name="Page"/> is -1.</param>
[DebuggerDisplay("PointerHover: page {Page}")]
public readonly record struct PointerHover(int Page, PagePoint Point)
{
    /// <summary>Gets a value indicating whether the pointer is over a page.</summary>
    public bool IsOverPage => Page >= 0;
}
