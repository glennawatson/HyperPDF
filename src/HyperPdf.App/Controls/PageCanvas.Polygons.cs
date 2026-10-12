// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>
/// The polygon, cloud and connected lines tools: each click adds a corner, the next side follows the pointer, and a
/// double click or Enter finishes. Backspace takes the last corner back and Escape starts again.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The radius of a placed corner's dot.</summary>
    private const double CornerRadius = 3;

    /// <summary>The corners placed so far, in page space.</summary>
    private readonly List<PagePoint> _corners = [];

    /// <summary>The page the corners are on, or -1.</summary>
    private int _cornerPage = -1;

    /// <summary>Where the pointer is, in page space, so the next side follows it.</summary>
    private PagePoint _cornerHover;

    /// <summary>Adds a corner when a polygon tool is active; a double click finishes the shape.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page pressed, or -1.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when a polygon tool used the press.</returns>
    private bool PressPolygon(DocumentTabViewModel tab, int page, Point position, PointerPressedEventArgs e)
    {
        if (AnnotationsViewModel.GetPolygonKind(tab.Annotations.Tool) is null)
        {
            CancelPolygon();
            return false;
        }

        if (page < 0 || (_cornerPage >= 0 && page != _cornerPage))
        {
            return true;
        }

        if (e.ClickCount >= DoubleClick)
        {
            _ = FinishPolygon(tab);
            return true;
        }

        _cornerPage = page;
        _cornerHover = ToPage(tab, page, position);
        _corners.Add(_cornerHover);
        InvalidateVisual();
        return true;
    }

    /// <summary>Moves the next side's free end with the pointer.</summary>
    /// <param name="position">The canvas point.</param>
    private void HoverPolygon(Point position)
    {
        if (_cornerPage < 0 || Tab is not { } tab)
        {
            return;
        }

        _cornerHover = ToPage(tab, _cornerPage, position);
        InvalidateVisual();
    }

    /// <summary>Adds the shape from the placed corners and starts again.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns><see langword="true"/> when there were corners to finish.</returns>
    private bool FinishPolygon(DocumentTabViewModel tab)
    {
        if (_cornerPage < 0)
        {
            return false;
        }

        if (AnnotationsViewModel.GetPolygonKind(tab.Annotations.Tool) is { } kind)
        {
            _ = tab.Annotations.AddPolygon(_cornerPage, kind, CollectionsMarshal.AsSpan(_corners));
        }

        CancelPolygon();
        return true;
    }

    /// <summary>Forgets the placed corners.</summary>
    private void CancelPolygon()
    {
        if (_cornerPage < 0)
        {
            return;
        }

        _corners.Clear();
        _cornerPage = -1;
        InvalidateVisual();
    }

    /// <summary>Handles Enter (finish), Backspace (take back a corner) and Escape (start again) while placing corners.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandlePolygonKey(Key key)
    {
        if (_cornerPage < 0 || Tab is not { } tab)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
            {
                return FinishPolygon(tab);
            }

            case Key.Escape:
            {
                CancelPolygon();
                return true;
            }

            case Key.Back:
            {
                _corners.RemoveAt(_corners.Count - 1);
                if (_corners.Count == 0)
                {
                    _cornerPage = -1;
                }

                InvalidateVisual();
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Draws the placed corners, the sides between them and the side that follows the pointer.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawPolygonPreview(DrawingContext context, DocumentTabViewModel tab)
    {
        if (_cornerPage < 0 || _cornerPage >= _sizes.Length || _corners.Count == 0)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(_cornerPage), _sizes[_cornerPage], tab.Rotation, _layout.Options.Scale);
        var previous = transform.ToCanvas(_corners[0]);
        foreach (var corner in _corners)
        {
            var next = transform.ToCanvas(corner);
            context.DrawLine(StrokePen, previous, next);
            context.DrawEllipse(StrokePen.Brush, null, next, CornerRadius, CornerRadius);
            previous = next;
        }

        context.DrawLine(StrokePen, previous, transform.ToCanvas(_cornerHover));
    }
}
