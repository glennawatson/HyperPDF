// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// The text of an element for a screen reader that reads by character, word and line. Lines end at a line feed. The caret and the
/// selection are offsets in UTF-16 code units, which is what AT-SPI and UI Automation count.
/// </summary>
/// <param name="Value">The text.</param>
/// <param name="CaretOffset">The caret's offset from the start, or -1 when the element has no caret.</param>
/// <param name="SelectionStart">The offset where the selection starts, or -1 when nothing is selected.</param>
/// <param name="SelectionEnd">The offset just after the selection, or -1 when nothing is selected.</param>
[DebuggerDisplay("AccessibleText: {Value.Length} characters, caret {CaretOffset}")]
public sealed record AccessibleText(string Value, int CaretOffset, int SelectionStart, int SelectionEnd)
{
    /// <summary>Creates text with no caret and no selection.</summary>
    /// <param name="value">The characters to hold.</param>
    /// <returns>The text with nothing marked.</returns>
    public static AccessibleText Plain(string value) => new(value, -1, -1, -1);
}
