// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>One styled text segment on a wrapped line.</summary>
/// <param name="Source">The string containing the characters.</param>
/// <param name="Start">The start index in <paramref name="Source"/>.</param>
/// <param name="Length">The number of characters.</param>
/// <param name="Width">The measured advance in points.</param>
/// <param name="Font">The font to draw with.</param>
/// <param name="Size">The font size in points.</param>
/// <param name="Color">The RGB colour.</param>
[DebuggerDisplay("StyledSegment: {Start}+{Length}, {Width} pt")]
internal readonly record struct StyledSegment(
    string Source,
    int Start,
    int Length,
    float Width,
    PdfCanvasFont Font,
    float Size,
    PdfRgbColor Color);
