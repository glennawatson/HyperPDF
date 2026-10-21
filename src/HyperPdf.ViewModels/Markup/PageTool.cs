// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>What dragging on the pages does, outside annotating.</summary>
public enum PageTool
{
    /// <summary>Dragging selects text; clicks follow links.</summary>
    SelectText = 0,

    /// <summary>Dragging moves the pages, like pushing paper with a hand; clicks still follow links.</summary>
    Hand = 1,

    /// <summary>Dragging a rectangle zooms until it fills the view.</summary>
    ZoomArea = 2,

    /// <summary>Dragging a rectangle copies a picture of it.</summary>
    Snapshot = 3,
}
