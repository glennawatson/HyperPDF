// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Viewer.Generator;

/// <summary>
/// Rewrites SVG path data, as an image editor saves it, with only the absolute commands M, L, C, A and Z that the viewer reads.
/// Relative commands become absolute, level and upright lines become lines, and smooth and quadratic curves become cubic curves.
/// </summary>
/// <param name="output">Receives the rewritten path data.</param>
internal sealed class PathNormalizer(StringBuilder output)
{
    /// <summary>The share of the way to its control point that a quadratic curve's cubic control points lie.</summary>
    private const double QuadraticShare = 2.0 / 3.0;

    /// <summary>The pen has no control point to mirror.</summary>
    private const int NoControl = 0;

    /// <summary>The control point kept is the second one of a cubic curve.</summary>
    private const int CubicControl = 1;

    /// <summary>The control point kept is the one of a quadratic curve.</summary>
    private const int QuadraticControl = 2;

    /// <summary>Where the pen is.</summary>
    private SvgPoint _pen;

    /// <summary>Where the open line started, which a close goes back to.</summary>
    private SvgPoint _start;

    /// <summary>The last control point, which a smooth curve mirrors.</summary>
    private SvgPoint _control;

    /// <summary>Which kind of curve the last control point belongs to.</summary>
    private int _controlKind;

    /// <summary>Rewrites one piece of path data after what was written before it.</summary>
    /// <param name="data">The path data.</param>
    /// <exception cref="FormatException">The data has a command the viewer cannot draw, or is cut short.</exception>
    internal void Append(string data)
    {
        var scanner = new PathScanner(data);
        while (scanner.TryReadCommand(out var command))
        {
            Apply(scanner, command);

            // Numbers after a command repeat it, and after a move they are lines.
            var repeated = command switch
            {
                'M' => 'L',
                'm' => 'l',
                _ => command,
            };
            while (char.ToUpperInvariant(command) != 'Z' && scanner.HasNumber())
            {
                Apply(scanner, repeated);
            }
        }
    }

    /// <summary>Writes a move to a point.</summary>
    /// <param name="point">The point.</param>
    internal void MoveTo(SvgPoint point)
    {
        _start = point;
        _pen = point;
        _controlKind = NoControl;
        Write('M', point);
    }

    /// <summary>Writes a line to a point.</summary>
    /// <param name="point">The point.</param>
    internal void LineTo(SvgPoint point)
    {
        _pen = point;
        _controlKind = NoControl;
        Write('L', point);
    }

    /// <summary>Writes an arc of an ellipse to a point.</summary>
    /// <param name="radii">The radii of the ellipse.</param>
    /// <param name="turn">The turn of the ellipse in degrees.</param>
    /// <param name="flags">The larger arc flag in X and the sweep flag in Y, each 0 or 1.</param>
    /// <param name="point">The end point.</param>
    internal void ArcTo(SvgPoint radii, double turn, SvgPoint flags, SvgPoint point)
    {
        _pen = point;
        _controlKind = NoControl;
        _ = output.Append("A ").Append(Text(radii.X)).Append(',').Append(Text(radii.Y)).Append(' ').Append(Text(turn)).Append(' ')
            .Append(Text(flags.X)).Append(' ').Append(Text(flags.Y)).Append(' ').Append(Text(point.X)).Append(',').Append(Text(point.Y)).Append(' ');
    }

    /// <summary>Writes a close of the open line.</summary>
    internal void Close()
    {
        _pen = _start;
        _controlKind = NoControl;
        _ = output.Append("Z ");
    }

    /// <summary>Writes a number with at most three decimals, as every culture reads it.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Text(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Reads the numbers of one command and writes it.</summary>
    /// <param name="scanner">The path data.</param>
    /// <param name="command">The command's letter, small when its numbers are measured from the pen.</param>
    /// <exception cref="FormatException">The viewer cannot draw the command.</exception>
    private void Apply(PathScanner scanner, char command)
    {
        var relative = char.IsLower(command);
        var letter = char.ToUpperInvariant(command);
        if (!ApplyLine(scanner, letter, relative) && !ApplyCurve(scanner, letter, relative))
        {
            throw new FormatException($"the path command '{command}' is not one an icon can use");
        }
    }

    /// <summary>Writes a move, a line or a close.</summary>
    /// <param name="scanner">The path data.</param>
    /// <param name="letter">The command's capital letter.</param>
    /// <param name="relative">Whether the numbers are measured from the pen.</param>
    /// <returns><see langword="true"/> when the command was one of these.</returns>
    private bool ApplyLine(PathScanner scanner, char letter, bool relative)
    {
        switch (letter)
        {
            case 'M':
                {
                    MoveTo(Point(scanner, relative));
                    return true;
                }

            case 'L':
                {
                    LineTo(Point(scanner, relative));
                    return true;
                }

            case 'H':
                {
                    LineTo(new(scanner.ReadNumber() + (relative ? _pen.X : 0), _pen.Y));
                    return true;
                }

            case 'V':
                {
                    LineTo(new(_pen.X, scanner.ReadNumber() + (relative ? _pen.Y : 0)));
                    return true;
                }

            case 'Z':
                {
                    Close();
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Writes a curve or an arc.</summary>
    /// <param name="scanner">The path data.</param>
    /// <param name="letter">The command's capital letter.</param>
    /// <param name="relative">Whether the numbers are measured from the pen.</param>
    /// <returns><see langword="true"/> when the command was one of these.</returns>
    private bool ApplyCurve(PathScanner scanner, char letter, bool relative)
    {
        switch (letter)
        {
            case 'C':
                {
                    var first = Point(scanner, relative);
                    var second = Point(scanner, relative);
                    Cubic(first, second, Point(scanner, relative));
                    return true;
                }

            case 'S':
                {
                    var first = Mirrored(CubicControl);
                    var second = Point(scanner, relative);
                    Cubic(first, second, Point(scanner, relative));
                    return true;
                }

            case 'Q':
                {
                    var control = Point(scanner, relative);
                    Quadratic(control, Point(scanner, relative));
                    return true;
                }

            case 'T':
                {
                    Quadratic(Mirrored(QuadraticControl), Point(scanner, relative));
                    return true;
                }

            case 'A':
                {
                    var radii = new SvgPoint(scanner.ReadNumber(), scanner.ReadNumber());
                    var turn = scanner.ReadNumber();
                    var flags = new SvgPoint(scanner.ReadFlag() ? 1 : 0, scanner.ReadFlag() ? 1 : 0);
                    ArcTo(radii, turn, flags, Point(scanner, relative));
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Reads a point, measured from the pen when the command is relative.</summary>
    /// <param name="scanner">The path data.</param>
    /// <param name="relative">Whether the numbers are measured from the pen.</param>
    /// <returns>The point.</returns>
    private SvgPoint Point(PathScanner scanner, bool relative)
    {
        var x = scanner.ReadNumber();
        var y = scanner.ReadNumber();
        return relative ? new(_pen.X + x, _pen.Y + y) : new(x, y);
    }

    /// <summary>Finds the first control point of a smooth curve: the last one mirrored in the pen, or the pen when the curve before was of another kind.</summary>
    /// <param name="kind">The kind of curve being written.</param>
    /// <returns>The control point.</returns>
    private SvgPoint Mirrored(int kind) =>
        _controlKind == kind ? new(_pen.X + _pen.X - _control.X, _pen.Y + _pen.Y - _control.Y) : _pen;

    /// <summary>Writes a cubic curve and keeps its second control point.</summary>
    /// <param name="first">The first control point.</param>
    /// <param name="second">The second control point.</param>
    /// <param name="end">The end point.</param>
    private void Cubic(SvgPoint first, SvgPoint second, SvgPoint end)
    {
        _ = output.Append("C ").Append(Text(first.X)).Append(',').Append(Text(first.Y)).Append(' ')
            .Append(Text(second.X)).Append(',').Append(Text(second.Y)).Append(' ').Append(Text(end.X)).Append(',').Append(Text(end.Y)).Append(' ');
        _pen = end;
        _control = second;
        _controlKind = CubicControl;
    }

    /// <summary>Writes a quadratic curve as the cubic curve that follows the same line, and keeps its control point.</summary>
    /// <param name="control">The control point.</param>
    /// <param name="end">The end point.</param>
    private void Quadratic(SvgPoint control, SvgPoint end)
    {
        var from = _pen;
        var first = new SvgPoint(from.X + ((control.X - from.X) * QuadraticShare), from.Y + ((control.Y - from.Y) * QuadraticShare));
        var second = new SvgPoint(end.X + ((control.X - end.X) * QuadraticShare), end.Y + ((control.Y - end.Y) * QuadraticShare));
        Cubic(first, second, end);
        _control = control;
        _controlKind = QuadraticControl;
    }

    /// <summary>Writes a command that takes one point.</summary>
    /// <param name="command">The command's letter.</param>
    /// <param name="point">The point.</param>
    private void Write(char command, SvgPoint point) =>
        _ = output.Append(command).Append(' ').Append(Text(point.X)).Append(',').Append(Text(point.Y)).Append(' ');
}
