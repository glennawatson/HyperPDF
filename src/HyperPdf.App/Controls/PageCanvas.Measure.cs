// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Measuring;

namespace HyperPdf.App.Controls;

/// <summary>
/// The measuring tool on the page canvas: clicks add points, the last point follows the pointer, a double click or
/// Enter finishes and Escape clears; the lines, points and the measurement are drawn over the page.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The radius of a measured point's dot.</summary>
    private const double MeasurePointRadius = 3;

    /// <summary>The size of the measurement label's text.</summary>
    private const double MeasureLabelSize = 13;

    /// <summary>How far the label sits from the last point.</summary>
    private const double MeasureLabelOffset = 10;

    /// <summary>The padding around the label's text.</summary>
    private const double MeasureLabelPadding = 4;

    /// <summary>A double click.</summary>
    private const int DoubleClick = 2;

    /// <summary>Draws a measurement's lines and points.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="pen">The pen.</param>
    /// <param name="transform">The page transform.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="closed">Whether the shape closes back to its first point.</param>
    /// <returns>The last point, in canvas space.</returns>
    private static Point DrawMeasureLines(DrawingContext context, IPen pen, in PageTransform transform, IReadOnlyList<HyperPdf.Core.Geometry.PagePoint> points, bool closed)
    {
        var previous = transform.ToCanvas(points[0]);
        var first = previous;
        for (var i = 1; i < points.Count; i++)
        {
            var next = transform.ToCanvas(points[i]);
            context.DrawLine(pen, previous, next);
            previous = next;
        }

        if (closed && points.Count > 2)
        {
            context.DrawLine(pen, previous, first);
        }

        foreach (var point in points)
        {
            context.DrawEllipse(pen.Brush, null, transform.ToCanvas(point), MeasurePointRadius, MeasurePointRadius);
        }

        return previous;
    }

    /// <summary>Handles a press while measuring.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> while measuring, so the press does not select text.</returns>
    private bool BeginMeasurePress(Point position, PointerPressedEventArgs e)
    {
        if (Tab is not { Measure: { IsOn: true } measure } tab)
        {
            return false;
        }

        var page = _layout.HitTest(position.X, position.Y);
        if (page < 0)
        {
            return true;
        }

        if (e.ClickCount >= DoubleClick)
        {
            measure.Finish();
        }
        else
        {
            measure.AddPoint(page, ToPage(tab, page, position));
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Moves the measurement's last point with the pointer.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> while measuring.</returns>
    private bool ContinueMeasure(Point position)
    {
        if (Tab is not { Measure: { IsOn: true } measure } tab)
        {
            return false;
        }

        Cursor = CrossCursor;
        var page = _layout.HitTest(position.X, position.Y);
        if (page >= 0 && measure.MovePoint(page, ToPage(tab, page, position)))
        {
            InvalidateVisual();
        }

        return true;
    }

    /// <summary>Handles Enter (finish) and Escape (clear) while measuring.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandleMeasureKey(Key key)
    {
        if (Tab is not { Measure: { IsOn: true } measure })
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
            {
                measure.Finish();
                break;
            }

            case Key.Escape:
            {
                measure.Clear();
                break;
            }

            default:
            {
                return false;
            }
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Draws the measurement: its lines, its points and the measurement beside the last point.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawMeasurement(DrawingContext context, DocumentTabViewModel tab)
    {
        var measure = tab.Measure;
        var points = measure.Points;
        if (!measure.IsOn || measure.Page < 0 || measure.Page >= _sizes.Length || points.Count == 0 || _currentHitPen is not { } pen)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(measure.Page), _sizes[measure.Page], tab.Rotation, _layout.Options.Scale);
        var last = DrawMeasureLines(context, pen, transform, points, measure.Mode == MeasureMode.Area);
        if (measure.Result.Length > 0)
        {
            DrawMeasureLabel(context, pen, measure.Result, last);
        }
    }

    /// <summary>Draws the measurement beside the last point, in the window's colours, framed by the measuring line's colour.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="outline">The measurement's pen, which frames the label.</param>
    /// <param name="result">The measurement.</param>
    /// <param name="last">The last point, in canvas space.</param>
    private void DrawMeasureLabel(DrawingContext context, IPen outline, string result, Point last)
    {
        var text = new FormattedText(result, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, MeasureLabelSize, LabelForeground ?? outline.Brush);
        var origin = last + new Vector(MeasureLabelOffset, MeasureLabelOffset);
        var box = new Rect(origin, new Size(text.Width, text.Height)).Inflate(MeasureLabelPadding);
        context.DrawRectangle(LabelBackground, outline, box, MeasureLabelPadding, MeasureLabelPadding);
        context.DrawText(text, origin);
    }
}
