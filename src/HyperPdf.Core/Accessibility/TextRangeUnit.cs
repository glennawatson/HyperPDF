// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// The sizes of stretch a screen reader moves a text range by on Windows and macOS. The values match the UI Automation
/// <c>TextUnit</c> enumeration, so the Windows bridge passes them straight through.
/// </summary>
public enum TextRangeUnit
{
    /// <summary>One character.</summary>
    Character = 0,

    /// <summary>A stretch that has the same formatting; the page text has one format, so it is treated as a paragraph.</summary>
    Format = 1,

    /// <summary>A word with the spaces after it.</summary>
    Word = 2,

    /// <summary>A line, up to and including its line feed.</summary>
    Line = 3,

    /// <summary>A paragraph, up to and including its line feed.</summary>
    Paragraph = 4,

    /// <summary>A page of the text; the element's text is one page, so it is treated as the whole document.</summary>
    Page = 5,

    /// <summary>All of the text.</summary>
    Document = 6,
}
