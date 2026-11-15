// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Turns a stroked path into the outline of the ink it lays down, as edges for the coverage buffer. The pen is a circle in the path's
/// own space, so under a matrix that stretches one way more than the other the stroke is thicker one way, as PDF asks.
/// </summary>
/// <remarks>
/// The outline is a set of overlapping polygons that all turn the same way: one for each line, one for each corner and one for each
/// end. The nonzero rule then fills their union, so no polygon has to be clipped against another.
/// </remarks>
[DebuggerDisplay("Stroker: half width {_half}")]
internal sealed class Stroker : IDisposable
{
    /// <summary>The line cap that rounds an end.</summary>
    private const int RoundCap = 1;

    /// <summary>The line cap that squares an end off half a width beyond it.</summary>
    private const int SquareCap = 2;

    /// <summary>The line join that rounds a corner.</summary>
    private const int RoundJoin = 1;

    /// <summary>The line join that cuts a corner off.</summary>
    private const int BevelJoin = 2;

    /// <summary>The most points of one arc.</summary>
    private const int MaxArcPoints = 64;

    /// <summary>The points a polygon may have: an arc, its two ends and the centre.</summary>
    private const int MaxPolygonPoints = MaxArcPoints + 4;

    /// <summary>The farthest an arc's chords may stray from the arc, in pixels.</summary>
    private const float ArcTolerance = 0.1F;

    /// <summary>The smallest radius, in pixels, used to choose how finely an arc is cut.</summary>
    private const float MinArcRadius = 0.5F;

    /// <summary>The largest angle between two points of an arc, in radians.</summary>
    private const float MaxArcStep = 1F;

    /// <summary>The length below which a line or a turn counts as none.</summary>
    private const float Tiny = 1E-6F;

    /// <summary>Half, to turn a width into the pen's radius.</summary>
    private const float Half = 0.5F;

    /// <summary>Two, in the miter formula.</summary>
    private const float Two = 2F;

    /// <summary>The most dashes cut from one line, which bounds the work for a pattern far smaller than the line.</summary>
    private const int MaxDashesPerLine = 100_000;

    /// <summary>The lines of one path command, kept in order so their end points can be read back.</summary>
    private readonly EdgeList _scratch = new() { KeepsLevelEdges = true };

    /// <summary>The points of the subpath being stroked, in device pixels.</summary>
    private readonly List<Vector2> _points = [];

    /// <summary>The points of the dash being cut.</summary>
    private readonly List<Vector2> _dash = [];

    /// <summary>The points of the polygon being added.</summary>
    private readonly Vector2[] _polygon = new Vector2[MaxPolygonPoints];

    /// <summary>The matrix from the path's space to device pixels.</summary>
    private Matrix3x2 _matrix;

    /// <summary>The matrix from device pixels back to the path's space.</summary>
    private Matrix3x2 _inverse;

    /// <summary>The style of the stroke.</summary>
    private StrokeStyle _style;

    /// <summary>The pen's radius in the path's space.</summary>
    private float _half;

    /// <summary>The angle between two points of an arc, in radians.</summary>
    private float _arcStep;

    /// <summary>Where the edges go.</summary>
    private EdgeList? _output;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _scratch.Dispose();

    /// <summary>Adds the outline of a stroked path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="matrix">The matrix from the path's space to device pixels.</param>
    /// <param name="style">How the line is stroked.</param>
    /// <param name="output">Receives the edges, to be filled with the nonzero rule.</param>
    internal void Stroke(PdfPath path, Matrix3x2 matrix, in StrokeStyle style, EdgeList output)
    {
        if (!Matrix3x2.Invert(matrix, out _inverse))
        {
            return;
        }

        // The geometric mean of the two stretches is how many pixels one unit of the path covers.
        var scale = MathF.Sqrt(MathF.Abs(matrix.GetDeterminant()));
        _matrix = matrix;
        _style = style;
        _output = output;

        // A line thinner than a pixel is drawn one pixel wide, the thinnest the device shows.
        _half = MathF.Max(style.Width, 1F / MathF.Max(scale, Tiny)) * Half;
        var radius = MathF.Max(_half * scale, MinArcRadius);
        _arcStep = MathF.Min(MaxArcStep, Two * MathF.Acos(1F - (ArcTolerance / radius)));
        _points.Clear();
        var start = default(Vector2);
        foreach (ref readonly var command in path.Commands)
        {
            start = Take(command, start);
        }

        Flush(false);
    }

    /// <summary>Takes one path command: it starts, extends or closes the subpath being stroked.</summary>
    /// <param name="command">The command.</param>
    /// <param name="start">Where the subpath began, in device pixels.</param>
    /// <returns>Where the subpath the next command belongs to began.</returns>
    private Vector2 Take(in PdfPathCommand command, Vector2 start)
    {
        if (command.Kind == PdfPathCommandKind.MoveTo)
        {
            Flush(false);
            start = PathFlattener.Map(command.Point, _matrix);
            _points.Add(start);
            return start;
        }

        if (command.Kind == PdfPathCommandKind.Close)
        {
            Flush(true);

            // A line drawn after a close starts where the closed subpath began.
            _points.Add(start);
            return start;
        }

        if (_points.Count == 0)
        {
            return start;
        }

        _scratch.Clear();
        _ = PathFlattener.AddSegment(command, _matrix, _points[^1], _scratch);
        var lines = _scratch.Values;
        for (var i = 0; i < lines.Length; i += EdgeList.Stride)
        {
            _points.Add(new(lines[i + EdgeList.Stride - 1 - 1], lines[i + EdgeList.Stride - 1]));
        }

        return start;
    }

    /// <summary>Strokes the subpath gathered so far and empties it.</summary>
    /// <param name="closed">Whether the subpath ends where it began, so it has a corner there and no ends.</param>
    private void Flush(bool closed)
    {
        var points = CollectionsMarshal.AsSpan(_points);
        if (!points.IsEmpty)
        {
            if (HasDashes())
            {
                StrokeDashed(points, closed);
            }
            else
            {
                StrokeLine(points, closed);
            }
        }

        _points.Clear();
    }

    /// <summary>Tells whether the style has a dash pattern that leaves gaps.</summary>
    /// <returns><see langword="true"/> when the pattern has a length above zero.</returns>
    private bool HasDashes()
    {
        foreach (var length in _style.Dash.Span)
        {
            if (length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Strokes a run of points as one solid line.</summary>
    /// <param name="points">The points in device pixels.</param>
    /// <param name="closed">Whether the last point joins the first.</param>
    private void StrokeLine(ReadOnlySpan<Vector2> points, bool closed)
    {
        var first = default(Vector2);
        var previous = default(Vector2);
        var count = 0;
        var segments = closed ? points.Length : points.Length - 1;
        for (var i = 0; i < segments; i++)
        {
            var from = points[i];
            var to = points[(i + 1) % points.Length];
            if (!TryNormal(to - from, out var normal))
            {
                continue;
            }

            AddBody(from, to, normal);
            if (count > 0)
            {
                AddJoin(from, previous, normal);
            }

            first = count == 0 ? normal : first;
            previous = normal;
            count++;
        }

        if (count == 0)
        {
            AddDot(points[0]);
        }
        else if (closed)
        {
            AddJoin(points[0], previous, first);
        }
        else
        {
            AddCap(points[0], first, false);
            AddCap(points[^1], previous, true);
        }
    }

    /// <summary>Cuts a run of points into dashes and strokes each.</summary>
    /// <param name="points">The points in device pixels.</param>
    /// <param name="closed">Whether the last point joins the first.</param>
    private void StrokeDashed(ReadOnlySpan<Vector2> points, bool closed)
    {
        var pattern = _style.Dash.Span;
        var state = DashState.Start(pattern, _style.DashPhase);
        _dash.Clear();
        var segments = closed ? points.Length : points.Length - 1;
        for (var i = 0; i < segments; i++)
        {
            state = CutLine(points[i], points[(i + 1) % points.Length], pattern, state);
        }

        if (_dash.Count > 1)
        {
            StrokeLine(CollectionsMarshal.AsSpan(_dash), false);
        }

        _dash.Clear();
    }

    /// <summary>Cuts one line into the parts the dash pattern inks.</summary>
    /// <param name="from">The start of the line in device pixels.</param>
    /// <param name="to">The end of the line.</param>
    /// <param name="pattern">The dash and gap lengths in turn.</param>
    /// <param name="state">Where in the pattern the line starts.</param>
    /// <returns>Where in the pattern the line ends.</returns>
    private DashState CutLine(Vector2 from, Vector2 to, ReadOnlySpan<float> pattern, DashState state)
    {
        // Dash lengths are in the path's units, so the line is measured there.
        var length = Vector2.TransformNormal(to - from, _inverse).Length();
        if (length < Tiny)
        {
            return state;
        }

        var position = 0F;
        for (var cut = 0; cut < MaxDashesPerLine && position < length; cut++)
        {
            var take = MathF.Min(state.Remaining, length - position);
            if (state.IsInk)
            {
                if (_dash.Count == 0)
                {
                    _dash.Add(Vector2.Lerp(from, to, position / length));
                }

                _dash.Add(Vector2.Lerp(from, to, (position + take) / length));
            }

            position += take;
            state = state with { Remaining = state.Remaining - take };
            if (state.Remaining > Tiny)
            {
                continue;
            }

            EndDash(state.IsInk);
            state = state.Next(pattern);
        }

        return state;
    }

    /// <summary>Strokes the dash that just ended.</summary>
    /// <param name="wasInk">Whether the part of the pattern that ended was a dash rather than a gap.</param>
    private void EndDash(bool wasInk)
    {
        if (!wasInk)
        {
            return;
        }

        if (_dash.Count > 1)
        {
            StrokeLine(CollectionsMarshal.AsSpan(_dash), false);
        }

        _dash.Clear();
    }

    /// <summary>Finds the unit normal of a line in the path's space, pointing to its left.</summary>
    /// <param name="direction">The line's direction in device pixels.</param>
    /// <param name="normal">Receives the normal.</param>
    /// <returns><see langword="false"/> when the line has no length.</returns>
    private bool TryNormal(Vector2 direction, out Vector2 normal)
    {
        var user = Vector2.TransformNormal(direction, _inverse);
        var length = user.Length();
        if (length < Tiny || !float.IsFinite(length))
        {
            normal = default;
            return false;
        }

        normal = new(-user.Y / length, user.X / length);
        return true;
    }

    /// <summary>Maps a vector of the pen, in radii, to device pixels.</summary>
    /// <param name="unit">The vector, where a length of one is the pen's radius.</param>
    /// <returns>The vector in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2 Pen(Vector2 unit) => Vector2.TransformNormal(unit * _half, _matrix);

    /// <summary>Adds the rectangle of ink along one line.</summary>
    /// <param name="from">The start of the line.</param>
    /// <param name="to">The end of the line.</param>
    /// <param name="normal">The line's normal in the path's space.</param>
    private void AddBody(Vector2 from, Vector2 to, Vector2 normal)
    {
        var offset = Pen(normal);
        _polygon[0] = from + offset;
        _polygon[1] = to + offset;
        _polygon[1 + 1] = to - offset;
        _polygon[1 + 1 + 1] = from - offset;
        AddPolygon(1 + 1 + 1 + 1);
    }

    /// <summary>Adds the ink of a corner, on the side where the two lines leave a gap.</summary>
    /// <param name="corner">The corner in device pixels.</param>
    /// <param name="before">The normal of the line that arrives.</param>
    /// <param name="after">The normal of the line that leaves.</param>
    private void AddJoin(Vector2 corner, Vector2 before, Vector2 after)
    {
        var turn = (before.X * after.Y) - (before.Y * after.X);
        var cosine = Vector2.Dot(before, after);
        if (MathF.Abs(turn) < Tiny && cosine > 0)
        {
            // The lines run on straight, so there is no gap.
            return;
        }

        // Normals point left, and the gap is on the right of a left turn.
        var side = turn > 0 ? -1F : 1F;
        var from = before * side;
        var to = after * side;
        _polygon[0] = corner;
        _polygon[1] = corner + Pen(from);
        var count = 1 + 1;
        if (_style.Join == RoundJoin)
        {
            // The arc turns the way the line turns: both outer normals are the line's normals with the same sign.
            count = AddArc(corner, from, MathF.Acos(Math.Clamp(cosine, -1F, 1F)) * (turn > 0 ? 1F : -1F), count);
        }
        else if (_style.Join != BevelJoin && cosine > Tiny - 1F && MathF.Sqrt(Two / (1F + cosine)) <= MathF.Max(_style.MiterLimit, 1F))
        {
            // The two outer edges meet at the tip of the miter.
            _polygon[count] = corner + Pen((from + to) / (1F + cosine));
            count++;
        }

        _polygon[count] = corner + Pen(to);
        AddPolygon(count + 1);
    }

    /// <summary>Adds the ink of an open end.</summary>
    /// <param name="end">The end in device pixels.</param>
    /// <param name="normal">The normal of the line at the end.</param>
    /// <param name="isLast">Whether this is the end the line arrives at rather than the one it leaves.</param>
    private void AddCap(Vector2 end, Vector2 normal, bool isLast)
    {
        // The direction out of the line: a quarter turn from the normal, one way at the start and the other at the end.
        var outward = isLast ? new Vector2(normal.Y, -normal.X) : new Vector2(-normal.Y, normal.X);
        if (_style.Cap == SquareCap)
        {
            _polygon[0] = end + Pen(normal);
            _polygon[1] = end + Pen(normal + outward);
            _polygon[1 + 1] = end + Pen(outward - normal);
            _polygon[1 + 1 + 1] = end - Pen(normal);
            AddPolygon(1 + 1 + 1 + 1);
        }
        else if (_style.Cap == RoundCap)
        {
            _polygon[0] = end + Pen(normal);
            var count = AddArc(end, normal, isLast ? -MathF.PI : MathF.PI, 1);
            _polygon[count] = end - Pen(normal);
            AddPolygon(count + 1);
        }
    }

    /// <summary>Adds the ink of a subpath with no length: a dot for round ends, a square for square ends, nothing for butt ends.</summary>
    /// <param name="point">The point in device pixels.</param>
    private void AddDot(Vector2 point)
    {
        if (_style.Cap == RoundCap)
        {
            _polygon[0] = point + Pen(Vector2.UnitX);
            AddPolygon(AddArc(point, Vector2.UnitX, MathF.Tau, 1));
        }
        else if (_style.Cap == SquareCap)
        {
            _polygon[0] = point + Pen(new(1F, 1F));
            _polygon[1] = point + Pen(new(-1F, 1F));
            _polygon[1 + 1] = point + Pen(new(-1F, -1F));
            _polygon[1 + 1 + 1] = point + Pen(new(1F, -1F));
            AddPolygon(1 + 1 + 1 + 1);
        }
    }

    /// <summary>Adds the points of an arc of the pen, after its first point and before its last.</summary>
    /// <param name="center">The centre in device pixels.</param>
    /// <param name="from">The unit vector the arc starts at, in the path's space.</param>
    /// <param name="sweep">The angle the arc turns through, in radians, positive towards the left normal.</param>
    /// <param name="count">The points of the polygon so far.</param>
    /// <returns>The points of the polygon with the arc's inner points added.</returns>
    private int AddArc(Vector2 center, Vector2 from, float sweep, int count)
    {
        var steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweep) / _arcStep), 1, MaxArcPoints);
        var sine = MathF.Sin(sweep / steps);
        var cosine = MathF.Cos(sweep / steps);
        var unit = from;
        for (var i = 1; i < steps; i++)
        {
            unit = new((unit.X * cosine) - (unit.Y * sine), (unit.X * sine) + (unit.Y * cosine));
            _polygon[count] = center + Pen(unit);
            count++;
        }

        return count;
    }

    /// <summary>Adds the polygon gathered, turned so its area is positive like every other polygon of the outline.</summary>
    /// <param name="count">The points of the polygon.</param>
    private void AddPolygon(int count)
    {
        var points = _polygon.AsSpan(0, count);
        var area = 0F;
        var last = points[^1];
        foreach (var point in points)
        {
            area += (last.X * point.Y) - (last.Y * point.X);
            last = point;
        }

        if (area < 0)
        {
            points.Reverse();
            last = points[^1];
        }

        foreach (var point in points)
        {
            _output!.Add(last.X, last.Y, point.X, point.Y);
            last = point;
        }
    }

    /// <summary>Where a line is in its dash pattern.</summary>
    /// <param name="Index">The part of the pattern in use.</param>
    /// <param name="Remaining">The length of that part still to come.</param>
    /// <param name="IsInk">Whether the part is a dash rather than a gap.</param>
    private readonly record struct DashState(int Index, float Remaining, bool IsInk)
    {
        /// <summary>Finds where a line starts in its pattern.</summary>
        /// <param name="pattern">The dash and gap lengths in turn.</param>
        /// <param name="phase">How far into the pattern the line starts.</param>
        /// <returns>The state at the start of the line.</returns>
        internal static DashState Start(ReadOnlySpan<float> pattern, float phase)
        {
            var state = new DashState(0, MathF.Max(pattern[0], 0), true);
            var total = 0F;
            foreach (var length in pattern)
            {
                total += MathF.Max(length, 0);
            }

            // An odd pattern repeats with dashes and gaps swapped, so its period is twice its sum.
            var period = (pattern.Length & 1) == 0 ? total : total + total;
            var left = float.IsFinite(phase) && phase > 0 ? phase % period : 0;
            while (left >= state.Remaining && left > 0)
            {
                left -= state.Remaining;
                state = state.Next(pattern);
            }

            return state with { Remaining = state.Remaining - left };
        }

        /// <summary>Moves to the next part of the pattern.</summary>
        /// <param name="pattern">The dash and gap lengths in turn.</param>
        /// <returns>The state at the start of the next part.</returns>
        internal DashState Next(ReadOnlySpan<float> pattern)
        {
            var index = (Index + 1) % pattern.Length;
            return new(index, MathF.Max(pattern[index], 0), !IsInk);
        }
    }
}
