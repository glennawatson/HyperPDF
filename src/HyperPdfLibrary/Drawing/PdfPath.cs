// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Drawing;

/// <summary>An immutable path made from managed drawing commands.</summary>
[DebuggerDisplay("PdfPath: {PointCount} points")]
public sealed class PdfPath
{
    /// <summary>The command array; the first <see cref="_count"/> commands are the path. An ordinary path owns the exact array and never changes it.</summary>
    private PdfPathCommand[] _commands;

    /// <summary>The number of commands in the path.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="PdfPath"/> class.</summary>
    /// <param name="commands">The immutable commands.</param>
    /// <param name="fillRule">The rule used to fill the path.</param>
    internal PdfPath(PdfPathCommand[] commands, PdfPathFillRule fillRule)
    {
        _commands = commands;
        _count = commands.Length;
        FillRule = fillRule;
        Measure();
    }

    /// <summary>Initializes a new instance of the <see cref="PdfPath"/> class that has no commands until it is lent some with <see cref="Lend"/>.</summary>
    internal PdfPath()
        : this([], PdfPathFillRule.Winding)
    {
    }

    /// <summary>Gets an empty winding path.</summary>
    public static PdfPath Empty { get; } = new([], PdfPathFillRule.Winding);

    /// <summary>Gets the fill rule.</summary>
    public PdfPathFillRule FillRule { get; private set; }

    /// <summary>Gets the bounds of the path's endpoints and control points.</summary>
    public PdfRect Bounds { get; private set; }

    /// <summary>Gets the number of endpoints and curve control points.</summary>
    public int PointCount { get; private set; }

    /// <summary>Gets whether the path has no line or curve segments.</summary>
    public bool IsEmpty { get; private set; }

    /// <summary>Gets the path commands as a read-only view.</summary>
    public ReadOnlySpan<PdfPathCommand> Commands => new(_commands, 0, _count);

    /// <summary>Returns a copy of the path transformed by a matrix.</summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>An independent path with transformed points.</returns>
    public PdfPath Transform(Matrix3x2 matrix)
    {
        if (_count == 0)
        {
            return FillRule == PdfPathFillRule.Winding ? Empty : new([], FillRule);
        }

        var transformed = new PdfPathCommand[_count];
        for (var i = 0; i < transformed.Length; i++)
        {
            transformed[i] = _commands[i].Transform(matrix);
        }

        return new(transformed, FillRule);
    }

    /// <summary>
    /// Points this path at commands that someone else owns, so a path can be passed to a device without copying them. The
    /// view lasts until the owner changes the commands; only the owner of this instance may use it, and only for a device
    /// that does not keep paths.
    /// </summary>
    /// <param name="commands">The owner's command array.</param>
    /// <param name="count">The number of commands in use.</param>
    /// <param name="fillRule">The rule used to fill the path.</param>
    internal void Lend(PdfPathCommand[] commands, int count, PdfPathFillRule fillRule)
    {
        _commands = commands;
        _count = count;
        FillRule = fillRule;
        Measure();
    }

    /// <summary>Calculates the bounds and point count of the commands in use.</summary>
    private void Measure()
    {
        var left = float.PositiveInfinity;
        var top = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        var bottom = float.NegativeInfinity;
        var pointCount = 0;
        var hasSegment = false;

        foreach (var command in Commands)
        {
            pointCount += command.PointCount;
            hasSegment |= command.Kind is PdfPathCommandKind.LineTo or PdfPathCommandKind.QuadraticTo or PdfPathCommandKind.CubicTo;
            for (var i = 0; i < command.PointCount; i++)
            {
                var point = command.GetPoint(i);
                left = MathF.Min(left, point.X);
                top = MathF.Min(top, point.Y);
                right = MathF.Max(right, point.X);
                bottom = MathF.Max(bottom, point.Y);
            }
        }

        Bounds = pointCount == 0 ? PdfRect.Empty : new(left, top, right, bottom);
        PointCount = pointCount;
        IsEmpty = !hasSegment;
    }
}
