// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>One line of wrapped text.</summary>
/// <param name="Start">The index of the line's first character in the source text.</param>
/// <param name="Length">The characters in the line, without the space or line break that ended it.</param>
/// <param name="Width">The line's advance in points.</param>
[DebuggerDisplay("PdfTextLine: {Start}+{Length}, {Width} pt")]
public readonly record struct PdfTextLine(int Start, int Length, float Width);
