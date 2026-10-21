// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>
/// The strokes drawn on a signature pad, for any toolkit: the finished strokes, the one being drawn, the most points a stroke
/// keeps and where a remembered mark sits in the pad. The toolkit passes pointer positions in and draws what it is given.
/// </summary>
[DebuggerDisplay("SignaturePadStrokes: {Drawn.Count} strokes")]
public sealed class SignaturePadStrokes
{
    /// <summary>The most points kept in one stroke.</summary>
    public static readonly int MaxPoints = 4096;

    /// <summary>Half, to centre a mark.</summary>
    private const double Half = 0.5;

    /// <summary>The stroke being drawn.</summary>
    private readonly List<PagePoint> _current = [];

    /// <summary>Gets the finished strokes, in pad coordinates.</summary>
    public List<PagePoint[]> Drawn { get; } = [];

    /// <summary>Gets a value indicating whether nothing has been drawn.</summary>
    public bool IsEmpty => Drawn.Count == 0 && _current.Count == 0;

    /// <summary>Gets the points of the stroke being drawn.</summary>
    public ReadOnlySpan<PagePoint> Current => CollectionsMarshal.AsSpan(_current);

    /// <summary>Gets a value indicating whether the stroke being drawn can take another point.</summary>
    public bool CanExtend => _current.Count < MaxPoints;

    /// <summary>Fits a mark inside a rectangle, keeping its shape, centred.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="area">The rectangle.</param>
    /// <returns>The mark's rectangle.</returns>
    public static LayoutRect Fit(SignatureMark mark, in LayoutRect area)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var scale = Math.Min(area.Width / mark.Width, area.Height / mark.Height);
        var width = mark.Width * scale;
        var height = mark.Height * scale;
        return new(area.X + ((area.Width - width) * Half), area.Y + ((area.Height - height) * Half), width, height);
    }

    /// <summary>Starts a stroke where the pointer was pressed.</summary>
    /// <param name="x">The horizontal place.</param>
    /// <param name="y">The vertical place.</param>
    public void Begin(double x, double y)
    {
        _current.Clear();
        _current.Add(new((float)x, (float)y));
    }

    /// <summary>Adds the pointer's place to the stroke, kept inside the pad, unless the stroke is full.</summary>
    /// <param name="x">The horizontal place.</param>
    /// <param name="y">The vertical place.</param>
    /// <param name="width">The pad's width.</param>
    /// <param name="height">The pad's height.</param>
    /// <returns><see langword="true"/> when the point was added.</returns>
    public bool Extend(double x, double y, double width, double height)
    {
        if (!CanExtend)
        {
            return false;
        }

        _current.Add(new((float)Math.Clamp(x, 0, width), (float)Math.Clamp(y, 0, height)));
        return true;
    }

    /// <summary>Finishes the stroke being drawn.</summary>
    /// <returns>The finished stroke, or <see langword="null"/> when none was started.</returns>
    public PagePoint[]? Finish()
    {
        if (_current.Count == 0)
        {
            return null;
        }

        PagePoint[] stroke = [.. _current];
        _current.Clear();
        Drawn.Add(stroke);
        return stroke;
    }

    /// <summary>Forgets every stroke.</summary>
    public void Clear()
    {
        Drawn.Clear();
        _current.Clear();
    }
}
