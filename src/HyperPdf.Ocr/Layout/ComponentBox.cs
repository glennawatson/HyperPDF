// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Layout;

/// <summary>The box around one connected blob of ink.</summary>
/// <param name="Left">The left edge, in pixels.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge, which is the last column of ink.</param>
/// <param name="Bottom">The bottom edge, which is the last row of ink.</param>
/// <param name="Pixels">The number of ink pixels.</param>
[DebuggerDisplay("ComponentBox: ({Left},{Top})-({Right},{Bottom}) {Pixels} pixels")]
public readonly record struct ComponentBox(int Left, int Top, int Right, int Bottom, int Pixels)
{
    /// <summary>Gets the number of columns the box covers.</summary>
    public int Width => Right - Left + 1;

    /// <summary>Gets the number of rows the box covers.</summary>
    public int Height => Bottom - Top + 1;
}
