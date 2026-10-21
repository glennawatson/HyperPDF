// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Input;

/// <summary>What a left pointer press does while a drawing tool, a mark being placed or a form is in play.</summary>
public enum DrawingPressKind
{
    /// <summary>The press belongs to selecting or another tool.</summary>
    None = 0,

    /// <summary>The press moves the signature or initials mark being placed.</summary>
    Placement = 1,

    /// <summary>The press starts dragging a shape or callout.</summary>
    Shape = 2,

    /// <summary>The press adds a corner to a polygon, cloud or connected lines.</summary>
    Polygon = 3,

    /// <summary>The press starts a freehand stroke.</summary>
    Ink = 4,

    /// <summary>The press activates a fillable form field.</summary>
    Field = 5,

    /// <summary>The press runs a click tool such as a note, text, stamp or caret.</summary>
    ClickTool = 6,
}
