// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Drawing;

/// <summary>Builds a path from ordered drawing commands.</summary>
[DebuggerDisplay("PdfPathBuilder: {_count} commands")]
public sealed class PdfPathBuilder
{
    /// <summary>The commands a new builder has room for.</summary>
    private const int InitialCapacity = 4;

    /// <summary>The ordered commands being built; the first <see cref="_count"/> are in use.</summary>
    private PdfPathCommand[] _commands = [];

    /// <summary>The number of commands in use.</summary>
    private int _count;

    /// <summary>Gets or sets the rule used to fill paths created by this builder.</summary>
    public PdfPathFillRule FillRule { get; set; }

    /// <summary>Starts a contour at a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveTo(float x, float y) => Add(new(PdfPathCommandKind.MoveTo, new(x, y), default, default));

    /// <summary>Adds a line to a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LineTo(float x, float y) => Add(new(PdfPathCommandKind.LineTo, new(x, y), default, default));

    /// <summary>Adds a quadratic curve to a point.</summary>
    /// <param name="controlX">The horizontal coordinate of the control point.</param>
    /// <param name="controlY">The vertical coordinate of the control point.</param>
    /// <param name="x">The horizontal coordinate of the endpoint.</param>
    /// <param name="y">The vertical coordinate of the endpoint.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QuadTo(float controlX, float controlY, float x, float y) =>
        Add(new(PdfPathCommandKind.QuadraticTo, new(x, y), new(controlX, controlY), default));

    /// <summary>Adds a cubic curve to a point.</summary>
    /// <param name="control1X">The horizontal coordinate of the first control point.</param>
    /// <param name="control1Y">The vertical coordinate of the first control point.</param>
    /// <param name="control2X">The horizontal coordinate of the second control point.</param>
    /// <param name="control2Y">The vertical coordinate of the second control point.</param>
    /// <param name="x">The horizontal coordinate of the endpoint.</param>
    /// <param name="y">The vertical coordinate of the endpoint.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y) =>
        Add(new(PdfPathCommandKind.CubicTo, new(x, y), new(control1X, control1Y), new(control2X, control2Y)));

    /// <summary>Closes the current contour.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Add(new(PdfPathCommandKind.Close, default, default, default));

    /// <summary>Clears all commands and restores the winding fill rule.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _count = 0;
        FillRule = PdfPathFillRule.Winding;
    }

    /// <summary>Adds a rectangular contour.</summary>
    /// <param name="rect">The rectangle.</param>
    public void AddRect(PdfRect rect)
    {
        MoveTo(rect.Left, rect.Top);
        LineTo(rect.Right, rect.Top);
        LineTo(rect.Right, rect.Bottom);
        LineTo(rect.Left, rect.Bottom);
        Close();
    }

    /// <summary>Appends another path's commands.</summary>
    /// <param name="path">The path to append.</param>
    /// <exception cref="ArgumentNullException">The path is <see langword="null"/>.</exception>
    public void AddPath(PdfPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var command in path.Commands)
        {
            Add(command);
        }
    }

    /// <summary>Appends another path after applying a transform to its points.</summary>
    /// <param name="path">The path to append.</param>
    /// <param name="matrix">The transform.</param>
    /// <exception cref="ArgumentNullException">The path is <see langword="null"/>.</exception>
    public void AddPath(PdfPath path, Matrix3x2 matrix)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var command in path.Commands)
        {
            Add(command.Transform(matrix));
        }
    }

    /// <summary>Transforms the points already added to this builder.</summary>
    /// <param name="matrix">The transform.</param>
    public void Transform(Matrix3x2 matrix)
    {
        for (var i = 0; i < _count; i++)
        {
            _commands[i] = _commands[i].Transform(matrix);
        }
    }

    /// <summary>Creates a path with an independent, exact-sized copy of the commands.</summary>
    /// <returns>The immutable path.</returns>
    public PdfPath Detach()
    {
        if (_count == 0)
        {
            return FillRule == PdfPathFillRule.Winding ? PdfPath.Empty : new([], FillRule);
        }

        var path = new PdfPath(_commands.AsSpan(0, _count).ToArray(), FillRule);
        _count = 0;
        return path;
    }

    /// <summary>
    /// Empties the builder and shows its commands through a reusable path, so a device that does not keep paths can be
    /// given one without a copy. The commands stay readable until the next command is added to this builder.
    /// </summary>
    /// <param name="scratch">The path to point at the commands.</param>
    /// <returns><paramref name="scratch"/>, now showing the commands.</returns>
    internal PdfPath Lend(PdfPath scratch)
    {
        scratch.Lend(_commands, _count, FillRule);
        _count = 0;
        return scratch;
    }

    /// <summary>Adds a command, making room for it first.</summary>
    /// <param name="command">The command.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Add(in PdfPathCommand command)
    {
        if (_count == _commands.Length)
        {
            Array.Resize(ref _commands, Math.Max(InitialCapacity, _count << 1));
        }

        _commands[_count] = command;
        _count++;
    }
}
