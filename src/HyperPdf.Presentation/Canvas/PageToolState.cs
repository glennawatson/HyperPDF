// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the page tools remember between pointer and key events: moving the pages, holding Space, dragging an area and auto-scrolling.</summary>
[DebuggerDisplay("PageToolState: panning={Panning}, space={SpaceHeld}, area={AreaDragging}")]
public sealed class PageToolState
{
    /// <summary>Gets or sets the annotation being moved or resized, or <see langword="null"/>.</summary>
    public AnnotationEditSession? ActiveEdit { get; set; }

    /// <summary>Gets or sets when the last auto-scroll frame ran, as a <see cref="Stopwatch"/> timestamp.</summary>
    public long AutoScrollLast { get; set; }

    /// <summary>Gets or sets a value indicating whether the Space bar is held for temporary hand panning.</summary>
    public bool SpaceHeld { get; set; }

    /// <summary>Gets or sets a value indicating whether a drag moved the pages during the current Space hold.</summary>
    public bool SpacePanned { get; set; }

    /// <summary>Gets or sets a value indicating whether a hand pan is in progress.</summary>
    public bool Panning { get; set; }

    /// <summary>Gets or sets where the pointer was in the scrolling area when the pan started.</summary>
    public CanvasPoint PanStart { get; set; }

    /// <summary>Gets or sets the scroll offset when the pan started.</summary>
    public CanvasPoint PanOffset { get; set; }

    /// <summary>Gets or sets a value indicating whether a snapshot or marquee-zoom rectangle is being dragged.</summary>
    public bool AreaDragging { get; set; }

    /// <summary>Gets or sets where the area drag started on the canvas.</summary>
    public CanvasPoint AreaStart { get; set; }

    /// <summary>Gets or sets where the pointer is now in the area drag.</summary>
    public CanvasPoint AreaEnd { get; set; }
}
