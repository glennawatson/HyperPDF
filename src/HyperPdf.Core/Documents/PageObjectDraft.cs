// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>A new object to write into a page's own content, placed and painted on the page as it is drawn.</summary>
/// <param name="Kind">What the object is.</param>
/// <param name="Bounds">Where the object goes, in page space with the top left corner of the page as the origin. For text the box gives the left edge and the top of the line.</param>
/// <param name="HasFill">Whether the object is filled.</param>
/// <param name="Fill">The fill colour as 0xRRGGBB, and the colour of text.</param>
/// <param name="HasStroke">Whether the object is outlined.</param>
/// <param name="Stroke">The line colour as 0xRRGGBB.</param>
/// <param name="LineWidth">The line width in points.</param>
/// <param name="Text">The words of a text object.</param>
/// <param name="FontSize">The size of text in points.</param>
/// <param name="Family">The family of font the text is set in.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic.</param>
[DebuggerDisplay("PageObjectDraft: {Kind} at {Bounds}")]
public readonly record struct PageObjectDraft(
    PageObjectDraftKind Kind,
    PageRect Bounds,
    bool HasFill,
    uint Fill,
    bool HasStroke,
    uint Stroke,
    float LineWidth,
    string Text,
    float FontSize,
    PageTextFamily Family,
    bool Bold,
    bool Italic);
