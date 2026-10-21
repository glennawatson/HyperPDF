// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Media;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;

namespace HyperPdf.App.Controls;

/// <summary>Draws the marks the layer over the pages plans on an Avalonia drawing context.</summary>
internal sealed class AvaloniaMarksPainter : IMarksPainter
{
    /// <summary>The thickness of the line under the word being read.</summary>
    private const double WordUnderline = 1.5;

    /// <summary>The layer whose brushes the marks use.</summary>
    private readonly PageMarksLayer _layer;

    /// <summary>The context the frame is drawn into.</summary>
    private DrawingContext? _context;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaMarksPainter"/> class.</summary>
    /// <param name="layer">The layer whose brushes the marks use.</param>
    internal AvaloniaMarksPainter(PageMarksLayer layer) => _layer = layer;

    /// <summary>Gets the context the frame is drawn into.</summary>
    private DrawingContext Context => _context ?? throw new InvalidOperationException("No frame is being drawn.");

    /// <inheritdoc/>
    public bool CanDraw(bool dim) => (dim ? _layer.DimBrush : _layer.SpokenBrush) is not null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawHit(in LayoutRect rect, bool current) =>
        Context.DrawRectangle(_layer.HitBrush, current ? _layer.CurrentHitPen : null, new(rect.X, rect.Y, rect.Width, rect.Height));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillSpoken(in LayoutRect rect) => Context.FillRectangle(_layer.SpokenBrush!, new(rect.X, rect.Y, rect.Width, rect.Height));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnderlineWord(CanvasPoint from, CanvasPoint to) =>
        Context.DrawLine(_layer.CurrentHitPen ?? new Pen(_layer.SpokenBrush, WordUnderline), new(from.X, from.Y), new(to.X, to.Y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillDim(in LayoutRect rect) => Context.FillRectangle(_layer.DimBrush!, new(rect.X, rect.Y, rect.Width, rect.Height));

    /// <summary>Starts a frame in a drawing context.</summary>
    /// <param name="context">The drawing context.</param>
    internal void Start(DrawingContext context) => _context = context;
}
