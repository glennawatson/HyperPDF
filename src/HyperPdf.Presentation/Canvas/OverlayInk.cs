// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Canvas;

/// <summary>Which pen or brush a mark drawn over the pages uses, so the toolkit alone decides what each looks like.</summary>
public enum OverlayInk
{
    /// <summary>The thin dark ink of shapes and strokes being drawn.</summary>
    Stroke = 0,

    /// <summary>The colour that outlines the current search hit; nothing is drawn where the toolkit has none.</summary>
    Hit = 1,

    /// <summary>The colour that outlines the current search hit, or the stroke ink where there is none.</summary>
    HitOrStroke = 2,

    /// <summary>The dashed outline of a place on a printed form that can be written on.</summary>
    Suggestion = 3,

    /// <summary>The outline of a place to write that the pointer is over.</summary>
    HoveredSuggestion = 4,

    /// <summary>The ring around the form field with the keyboard focus.</summary>
    FocusRing = 5,

    /// <summary>The fill of the text caret; nothing is drawn where the toolkit has none.</summary>
    Caret = 6,
}
