// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the annotation drawing tools remember between pointer events: an ink stroke, a shape being dragged, a click tool and the corners of a polygon.</summary>
[DebuggerDisplay("DrawingToolState: stroke page {StrokePage}, click page {ClickPage}")]
public sealed class DrawingToolState
{
    /// <summary>Gets the page points of the ink stroke being drawn.</summary>
    public List<PagePoint> Stroke { get; } = [];

    /// <summary>Gets the corners placed so far in a polygon, cloud or polyline.</summary>
    public PolygonDraft Polygon { get; } = new();

    /// <summary>Gets or sets the page an ink stroke is being drawn on, or -1 when not drawing.</summary>
    public int StrokePage { get; set; } = -1;

    /// <summary>Gets or sets the shape currently being dragged on a page, or <see langword="null"/>.</summary>
    public ShapePreview? ActiveShape { get; set; }

    /// <summary>Gets or sets the page clicked by a click-based annotation tool, or -1 when none.</summary>
    public int ClickPage { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether a click-based text tool snaps to a detected form region.</summary>
    public bool ClickSnaps { get; set; }

    /// <summary>Gets or sets the printed-form region suggestion under the pointer, or <see langword="null"/>.</summary>
    public HoveredFormRegion? HoveredRegion { get; set; }
}
