// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>
/// Shape tools on the page canvas: a drag draws a rectangle, ellipse, arrow or line, shown as it is dragged. The callout
/// tool is dragged the same way, from what it points at to where its text goes, then asks for the text.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The shortest drag, in points, that draws a shape; shorter drags are taken as stray clicks.</summary>
    private const float MinShapeDrag = 4;

    /// <summary>The shape being dragged, or <see langword="null"/>.</summary>
    private AnnotationKind? _shapeKind;

    /// <summary>The page the shape is dragged on.</summary>
    private int _shapePage = -1;

    /// <summary>Where the drag started, in page space.</summary>
    private PagePoint _shapeStart;

    /// <summary>Where the pointer is now, in page space.</summary>
    private PagePoint _shapeEnd;

    /// <summary>Lets the tools that draw take a press first: placing a signature, dragging a shape, or placing corners.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page under the pointer, or -1.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when one of them used the press.</returns>
    private bool BeginDrawingTool(DocumentTabViewModel tab, int page, Point position, PointerPressedEventArgs e) =>
        PressPlacement(tab, page, position) || BeginShape(tab, page, position, e) || PressPolygon(tab, page, position, e);

    /// <summary>Starts a shape when a shape tool is active.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page under the pointer, or -1.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when a shape was started.</returns>
    private bool BeginShape(DocumentTabViewModel tab, int page, Point position, PointerPressedEventArgs e)
    {
        if (page < 0 || AnnotationsViewModel.GetShapeKind(tab.Annotations.Tool) is not { } kind)
        {
            return false;
        }

        _shapeKind = kind;
        _shapePage = page;
        _shapeStart = ToPage(tab, page, position);
        _shapeEnd = _shapeStart;
        e.Pointer.Capture(this);
        return true;
    }

    /// <summary>Follows the pointer with the shape's free end.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> while a shape is dragged.</returns>
    private bool ContinueShape(Point position)
    {
        if (_shapeKind is null || Tab is not { } tab)
        {
            return false;
        }

        _shapeEnd = ToPage(tab, _shapePage, position);
        InvalidateVisual();
        return true;
    }

    /// <summary>Adds the dragged shape on release.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a shape was being dragged.</returns>
    private bool EndShape(DocumentTabViewModel tab, Point position)
    {
        if (_shapeKind is not { } kind)
        {
            return false;
        }

        _shapeKind = null;
        _shapeEnd = ToPage(tab, _shapePage, position);
        if (Math.Abs(_shapeEnd.X - _shapeStart.X) + Math.Abs(_shapeEnd.Y - _shapeStart.Y) < MinShapeDrag)
        {
            InvalidateVisual();
            return true;
        }

        if (kind == AnnotationKind.Callout)
        {
            _ = tab.Annotations.AddCalloutAsync(_shapePage, _shapeStart, _shapeEnd);
        }
        else
        {
            _ = tab.Annotations.AddShape(_shapePage, kind, _shapeStart, _shapeEnd);
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Draws the shape being dragged.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawShapePreview(DrawingContext context, DocumentTabViewModel tab)
    {
        if (_shapeKind is not { } kind || _shapePage < 0 || _shapePage >= _sizes.Length)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(_shapePage), _sizes[_shapePage], tab.Rotation, _layout.Options.Scale);
        var start = transform.ToCanvas(_shapeStart);
        var end = transform.ToCanvas(_shapeEnd);
        var box = new Rect(start, end).Normalize();
        switch (kind)
        {
            case AnnotationKind.Rectangle:
            {
                context.DrawRectangle(null, StrokePen, box);
                break;
            }

            case AnnotationKind.Ellipse:
            {
                context.DrawEllipse(null, StrokePen, box);
                break;
            }

            default:
            {
                context.DrawLine(StrokePen, start, end);
                break;
            }
        }
    }
}
