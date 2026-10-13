// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A rectangle in the drawing area of an appearance.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The lower edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("FormBox: {X}, {Y}, {Width} x {Height}")]
internal readonly record struct FormBox(float X, float Y, float Width, float Height)
{
    /// <summary>Gets the right edge.</summary>
    internal float Right => X + Width;

    /// <summary>Gets the upper edge.</summary>
    internal float Top => Y + Height;
}
