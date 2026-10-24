// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Layout;

/// <summary>The box around one line of text.</summary>
/// <param name="Left">The left edge, in pixels.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge, which is the last column of the line.</param>
/// <param name="Bottom">The bottom edge, which is the last row of the line.</param>
[DebuggerDisplay("TextLine: ({Left},{Top})-({Right},{Bottom})")]
public readonly record struct TextLine(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets the number of columns the line covers.</summary>
    public int Width => Right - Left + 1;

    /// <summary>Gets the number of rows the line covers.</summary>
    public int Height => Bottom - Top + 1;
}
