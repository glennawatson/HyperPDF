// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Copies immutable managed contours into recorder-owned native conversion state.</summary>
internal static class SkiaPathConversion
{
    /// <summary>The commands of a single straight segment: a move and a line.</summary>
    private const int LineCommands = 2;

    /// <summary>Creates a temporary native contour whose fill rule follows the managed path.</summary>
    /// <param name="builder">The recording device's reusable builder.</param>
    /// <param name="path">The managed contour.</param>
    /// <returns>The owned temporary contour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKPath Create(SKPathBuilder builder, PdfPath path) => Create(builder, path, path.FillRule == PdfPathFillRule.EvenOdd);

    /// <summary>Creates a temporary native contour using the requested fill rule.</summary>
    /// <param name="builder">The recording device's reusable builder.</param>
    /// <param name="path">The managed contour.</param>
    /// <param name="evenOdd">Whether to apply the even-odd rule.</param>
    /// <returns>The owned temporary contour.</returns>
    internal static SKPath Create(SKPathBuilder builder, PdfPath path, bool evenOdd)
    {
        foreach (var command in path.Commands)
        {
            AppendCommand(builder, command);
        }

        var native = builder.Detach();
        native.FillType = evenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        return native;
    }

    /// <summary>
    /// Recognises the contour <c>re</c> makes: a move, three lines and a close around an axis-aligned rectangle with area.
    /// Skia clips to such a path exactly as it clips to the rectangle, so a clip can skip the native path.
    /// </summary>
    /// <param name="path">The managed contour.</param>
    /// <param name="rectangle">Receives the rectangle.</param>
    /// <returns><see langword="true"/> when the contour is such a rectangle.</returns>
    internal static bool TryGetRectangle(PdfPath path, out SKRect rectangle)
    {
        rectangle = default;
        var commands = path.Commands;
        if (!HasRectangleKinds(commands))
        {
            return false;
        }

        var first = commands[0].Point;
        var opposite = commands[2].Point;
        if (!IsAxisAligned(first, commands[1].Point, opposite, commands[3].Point))
        {
            return false;
        }

        var left = MathF.Min(first.X, opposite.X);
        var right = MathF.Max(first.X, opposite.X);
        var top = MathF.Min(first.Y, opposite.Y);
        var bottom = MathF.Max(first.Y, opposite.Y);

        // A rectangle with no width or height, or an infinite or undefined one, stays a path.
        if (!(right > left) || !(bottom > top) || !float.IsFinite(right - left) || !float.IsFinite(bottom - top))
        {
            return false;
        }

        rectangle = new(left, top, right, bottom);
        return true;
    }

    /// <summary>
    /// Recognises a contour of one straight segment: a move and a line, with no close. Skia strokes such a path exactly as
    /// it strokes the line between the points, so a stroke can skip the native path.
    /// </summary>
    /// <param name="path">The managed contour.</param>
    /// <param name="from">Receives the start of the segment.</param>
    /// <param name="to">Receives the end of the segment.</param>
    /// <returns><see langword="true"/> when the contour is one segment of non-zero length.</returns>
    internal static bool TryGetLine(PdfPath path, out SKPoint from, out SKPoint to)
    {
        from = default;
        to = default;
        var commands = path.Commands;
        if (commands.Length != LineCommands || commands[0].Kind != PdfPathCommandKind.MoveTo || commands[1].Kind != PdfPathCommandKind.LineTo)
        {
            return false;
        }

        var start = commands[0].Point;
        var end = commands[1].Point;

        // A segment with no length draws a dot only when it has round or square caps, which stays a path.
        if (Same(start.X, end.X) && Same(start.Y, end.Y))
        {
            return false;
        }

        from = new(start.X, start.Y);
        to = new(end.X, end.Y);
        return true;
    }

    /// <summary>Determines whether the commands are a move, three lines and a close.</summary>
    /// <param name="commands">The commands.</param>
    /// <returns><see langword="true"/> when the kinds match.</returns>
    private static bool HasRectangleKinds(ReadOnlySpan<PdfPathCommand> commands)
    {
        ReadOnlySpan<PdfPathCommandKind> kinds = [PdfPathCommandKind.MoveTo, PdfPathCommandKind.LineTo, PdfPathCommandKind.LineTo, PdfPathCommandKind.LineTo, PdfPathCommandKind.Close];
        if (commands.Length != kinds.Length)
        {
            return false;
        }

        for (var i = 0; i < kinds.Length; i++)
        {
            if (commands[i].Kind != kinds[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether four corners walk the edges of an axis-aligned rectangle, starting along either axis.</summary>
    /// <param name="first">The first corner.</param>
    /// <param name="second">The second corner.</param>
    /// <param name="third">The corner opposite the first.</param>
    /// <param name="fourth">The fourth corner.</param>
    /// <returns><see langword="true"/> when each edge is horizontal or vertical.</returns>
    private static bool IsAxisAligned(PdfPoint first, PdfPoint second, PdfPoint third, PdfPoint fourth) =>
        (Same(first.Y, second.Y) && Same(second.X, third.X) && Same(third.Y, fourth.Y) && Same(fourth.X, first.X))
        || (Same(first.X, second.X) && Same(second.Y, third.Y) && Same(third.X, fourth.X) && Same(fourth.Y, first.Y));

    /// <summary>Determines whether two coordinates are the same value, bit for bit.</summary>
    /// <param name="left">One coordinate.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> when they are identical.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Same(float left, float right) => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);

    /// <summary>Appends one managed contour command.</summary>
    /// <param name="builder">The reusable native builder.</param>
    /// <param name="command">The managed command.</param>
    private static void AppendCommand(SKPathBuilder builder, PdfPathCommand command)
    {
        switch (command.Kind)
        {
            case PdfPathCommandKind.MoveTo:
                {
                    builder.MoveTo(command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.LineTo:
                {
                    builder.LineTo(command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.QuadraticTo:
                {
                    builder.QuadTo(command.Control1.X, command.Control1.Y, command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.CubicTo:
                {
                    builder.CubicTo(command.Control1.X, command.Control1.Y, command.Control2.X, command.Control2.Y, command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.Close:
                {
                    builder.Close();
                    break;
                }
        }
    }
}
