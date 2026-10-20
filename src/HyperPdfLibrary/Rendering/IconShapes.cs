// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Draws the small icons of note and file attachment annotations from shape tables. A table is text of path commands in a
/// unit square whose origin is the icon's upper-left corner and whose y axis points down, with every number in hundredths:
/// <c>M x y</c> moves, <c>L x y</c> draws a line, <c>C x1 y1 x2 y2 x3 y3</c> draws a curve and <c>Z</c> closes the path.
/// </summary>
internal static class IconShapes
{
    /// <summary>The width of the icon's outline and details, in points.</summary>
    private const float LineWidth = 1;

    /// <summary>The number of hundredths in a unit.</summary>
    private const float Hundredths = 100;

    /// <summary>The base of the numbers in a table.</summary>
    private const int Base = 10;

    /// <summary>The most numbers a command takes.</summary>
    private const int MaxArguments = 6;

    /// <summary>The numbers a move or line command takes.</summary>
    private const int PointArguments = 2;

    /// <summary>Draws an icon: its body filled and outlined, then its details as lines.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="bounds">The icon's rectangle; the icon is square, anchored at its upper-left corner.</param>
    /// <param name="fill">The fill of the body.</param>
    /// <param name="body">The body's path, or empty for an icon with no filled body.</param>
    /// <param name="detail">The path of the details drawn as lines, or empty for none.</param>
    internal static void Draw(ref PdfContentBuilder builder, PdfRectangle bounds, IconFill fill, ReadOnlySpan<byte> body, ReadOnlySpan<byte> detail)
    {
        builder.SetFillRgb(fill.Red, fill.Green, fill.Blue);
        builder.SetStrokeRgb(0, 0, 0);
        builder.SetLineWidth(LineWidth);
        Paint(ref builder, bounds, body, true);
        Paint(ref builder, bounds, detail, false);
    }

    /// <summary>Paints one table: filled and outlined, or as lines. An empty table paints nothing.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="bounds">The icon's rectangle.</param>
    /// <param name="table">The shape table.</param>
    /// <param name="filled">Whether to fill the path as well as outline it.</param>
    private static void Paint(ref PdfContentBuilder builder, PdfRectangle bounds, ReadOnlySpan<byte> table, bool filled)
    {
        if (table.IsEmpty)
        {
            return;
        }

        Trace(ref builder, bounds, table);
        if (filled)
        {
            builder.FillEvenOddAndStroke();
            return;
        }

        builder.Stroke();
    }

    /// <summary>Writes a shape table as a path.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="bounds">The icon's rectangle.</param>
    /// <param name="table">The shape table.</param>
    private static void Trace(ref PdfContentBuilder builder, PdfRectangle bounds, ReadOnlySpan<byte> table)
    {
        Span<float> arguments = stackalloc float[MaxArguments];
        var command = (byte)0;
        var count = 0;
        var i = 0;
        while (i < table.Length)
        {
            var c = table[i];
            if (c == (byte)' ')
            {
                i++;
                continue;
            }

            if (c is >= (byte)'A' and <= (byte)'Z')
            {
                command = c;
                count = 0;
                i++;
                if (command == (byte)'Z')
                {
                    builder.ClosePath();
                }

                continue;
            }

            arguments[count] = ReadNumber(table, ref i) / Hundredths;
            count++;
            if (count != ArgumentCount(command))
            {
                continue;
            }

            Run(ref builder, bounds, command, arguments);
            count = 0;
        }
    }

    /// <summary>Gets how many numbers a command takes.</summary>
    /// <param name="command">The command letter.</param>
    /// <returns>The count.</returns>
    private static int ArgumentCount(byte command) => command switch
    {
        (byte)'C' => MaxArguments,
        (byte)'M' or (byte)'L' => PointArguments,
        _ => 0,
    };

    /// <summary>Reads a whole number from a table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="i">The position of the first digit; moves past the last.</param>
    /// <returns>The number.</returns>
    private static int ReadNumber(ReadOnlySpan<byte> table, ref int i)
    {
        var value = 0;
        while (i < table.Length && table[i] is >= (byte)'0' and <= (byte)'9')
        {
            value = (value * Base) + (table[i] - (byte)'0');
            i++;
        }

        return value;
    }

    /// <summary>Writes one command with its numbers.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="bounds">The icon's rectangle.</param>
    /// <param name="command">The command letter.</param>
    /// <param name="a">The numbers, in units of the icon's side.</param>
    private static void Run(ref PdfContentBuilder builder, PdfRectangle bounds, byte command, scoped ReadOnlySpan<float> a)
    {
        var side = bounds.Width;
        switch (command)
        {
            case (byte)'M':
                {
                    builder.MoveTo(bounds.Left + (a[0] * side), bounds.Top - (a[1] * side));
                    break;
                }

            case (byte)'L':
                {
                    builder.LineTo(bounds.Left + (a[0] * side), bounds.Top - (a[1] * side));
                    break;
                }

            default:
                {
                    builder.CurveTo(
                        bounds.Left + (a[0] * side),
                        bounds.Top - (a[1] * side),
                        bounds.Left + (a[2] * side),
                        bounds.Top - (a[3] * side),
                        bounds.Left + (a[4] * side),
                        bounds.Top - (a[5] * side));
                    break;
                }
        }
    }
}
