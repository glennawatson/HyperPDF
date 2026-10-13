// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>A character while a text page is built.</summary>
/// <param name="Kind">Where the character came from.</param>
/// <param name="Code">The character code, or -1.</param>
/// <param name="Unicode">The character.</param>
/// <param name="Origin">The origin in user space.</param>
/// <param name="Box">The outline box in user space.</param>
/// <param name="LooseBox">The em-cell box in user space.</param>
/// <param name="Run">The run the character belongs to, which gives its font size, render mode and weight.</param>
/// <param name="Group">Numbers the glyph, generated space or replacement text the character came from; its characters stay together when a line is reordered.</param>
/// <param name="Line">The line the character is on, or -1 for line breaks.</param>
/// <param name="InText">Whether the character is part of the page text.</param>
[DebuggerDisplay("TextBuildChar: {Unicode} {Kind}")]
internal readonly record struct TextBuildChar(
    PdfTextCharKind Kind,
    int Code,
    char Unicode,
    Vector2 Origin,
    PdfRectangle Box,
    PdfRectangle LooseBox,
    int Run,
    int Group,
    int Line,
    bool InText);
