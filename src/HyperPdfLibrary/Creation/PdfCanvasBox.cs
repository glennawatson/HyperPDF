// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A rectangle on a page, in points, with the origin at the bottom left of the page.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The bottom edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("PdfCanvasBox: ({X}, {Y}) {Width} x {Height}")]
public readonly record struct PdfCanvasBox(float X, float Y, float Width, float Height)
{
    /// <summary>Gets the right edge.</summary>
    public float Right => X + Width;

    /// <summary>Gets the top edge.</summary>
    public float Top => Y + Height;
}
