// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Viewer.Text;

/// <summary>Receives a glyph outline in font units and adds it to a coverage buffer in pixels, with curves cut into short lines.</summary>
/// <param name="buffer">The coverage buffer.</param>
/// <param name="scale">The pixels of one font unit.</param>
/// <param name="originX">The x of the glyph origin in pixels.</param>
/// <param name="baseline">The y of the baseline in pixels, growing downwards.</param>
internal ref struct OutlinePen(CoverageBuffer buffer, float scale, float originX, float baseline) : IGlyphOutlineSink
{
    /// <summary>The lines a curve is cut into. Screen text is small, so a few lines are closer than a pixel to the curve.</summary>
    private const int CurveSteps = 6;

    /// <summary>The weight of a control point in the middle terms of a cubic curve.</summary>
    private const float CubicMiddle = 3F;

    /// <summary>The weight of the control point in the middle term of a quadratic curve.</summary>
    private const float QuadraticMiddle = 2F;

    /// <summary>The x where the open contour started, in pixels.</summary>
    private float _startX;

    /// <summary>The y where the open contour started.</summary>
    private float _startY;

    /// <summary>The x of the pen.</summary>
    private float _x;

    /// <summary>The y of the pen.</summary>
    private float _y;

    /// <summary>Whether a contour is open.</summary>
    private bool _open;

    /// <inheritdoc/>
    public void MoveTo(float x, float y)
    {
        Close();
        _startX = ToPixelX(x);
        _startY = ToPixelY(y);
        _x = _startX;
        _y = _startY;
        _open = true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LineTo(float x, float y) => LineToPixel(ToPixelX(x), ToPixelY(y));

    /// <inheritdoc/>
    public void QuadraticTo(float controlX, float controlY, float x, float y)
    {
        var fromX = _x;
        var fromY = _y;
        var midX = ToPixelX(controlX);
        var midY = ToPixelY(controlY);
        var toX = ToPixelX(x);
        var toY = ToPixelY(y);
        for (var step = 1; step <= CurveSteps; step++)
        {
            var t = step / (float)CurveSteps;
            var rest = 1F - t;
            var a = rest * rest;
            var b = QuadraticMiddle * rest * t;
            var c = t * t;
            LineToPixel((a * fromX) + (b * midX) + (c * toX), (a * fromY) + (b * midY) + (c * toY));
        }
    }

    /// <inheritdoc/>
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
        var fromX = _x;
        var fromY = _y;
        var firstX = ToPixelX(control1X);
        var firstY = ToPixelY(control1Y);
        var secondX = ToPixelX(control2X);
        var secondY = ToPixelY(control2Y);
        var toX = ToPixelX(x);
        var toY = ToPixelY(y);
        for (var step = 1; step <= CurveSteps; step++)
        {
            var t = step / (float)CurveSteps;
            var rest = 1F - t;
            var a = rest * rest * rest;
            var b = CubicMiddle * rest * rest * t;
            var c = CubicMiddle * rest * t * t;
            var d = t * t * t;
            LineToPixel((a * fromX) + (b * firstX) + (c * secondX) + (d * toX), (a * fromY) + (b * firstY) + (c * secondY) + (d * toY));
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (!_open)
        {
            return;
        }

        // An outline only fills correctly when every contour ends where it began.
        LineToPixel(_startX, _startY);
        _open = false;
    }

    /// <summary>Turns a font x into a pixel x.</summary>
    /// <param name="x">The x in font units.</param>
    /// <returns>The x in pixels.</returns>
    private readonly float ToPixelX(float x) => originX + (x * scale);

    /// <summary>Turns a font y, which grows upwards, into a pixel y.</summary>
    /// <param name="y">The y in font units.</param>
    /// <returns>The y in pixels.</returns>
    private readonly float ToPixelY(float y) => baseline - (y * scale);

    /// <summary>Adds a line from the pen to a point and moves the pen there.</summary>
    /// <param name="x">The x in pixels.</param>
    /// <param name="y">The y in pixels.</param>
    private void LineToPixel(float x, float y)
    {
        buffer.Line(_x, _y, x, y);
        _x = x;
        _y = y;
    }
}
