// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>What a press on the page does.</summary>
public enum AnnotationTool
{
    /// <summary>Select text, follow links and pick annotations.</summary>
    Select = 0,

    /// <summary>Selecting text highlights it.</summary>
    Highlight = 1,

    /// <summary>Selecting text underlines it.</summary>
    Underline = 2,

    /// <summary>Selecting text strikes it out.</summary>
    StrikeOut = 3,

    /// <summary>Dragging draws freehand.</summary>
    Draw = 4,

    /// <summary>Clicking adds a sticky note.</summary>
    Note = 5,

    /// <summary>Clicking writes text on the page.</summary>
    Text = 6,

    /// <summary>Dragging draws a rectangle.</summary>
    Rectangle = 9,

    /// <summary>Dragging draws an ellipse.</summary>
    Ellipse = 10,

    /// <summary>Dragging draws an arrow pointing where the drag ends.</summary>
    Arrow = 11,

    /// <summary>Dragging draws a straight line.</summary>
    Line = 12,

    /// <summary>Clicking places the chosen stamp.</summary>
    Stamp = 13,

    /// <summary>Dragging from a point to where the text goes writes a callout pointing at that point.</summary>
    Callout = 14,

    /// <summary>Clicking adds the corners of a polygon; Enter or a double-click finishes it.</summary>
    Polygon = 15,

    /// <summary>Clicking adds the corners of a cloud; Enter or a double-click finishes it.</summary>
    Cloud = 16,

    /// <summary>Clicking adds the points of connected lines; Enter or a double-click finishes them.</summary>
    PolyLine = 17,

    /// <summary>Dragging marks an area to redact.</summary>
    Redact = 18,

    /// <summary>Selecting text marks it to redact.</summary>
    RedactText = 19,

    /// <summary>Clicking marks a place where text should be inserted.</summary>
    Caret = 20,

    /// <summary>Dragging marks over the page with a wide, see-through highlighter pen.</summary>
    FreehandHighlight = 21,
}
