// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Export;

/// <summary>Writes PostScript operators and operands as text, 7-bit ASCII with a line feed after each line.</summary>
/// <param name="output">The text destination.</param>
[DebuggerDisplay("PostScriptWriter")]
internal sealed class PostScriptWriter(TextWriter output)
{
    /// <summary>The longest line of encoded image data.</summary>
    private const int DataLineLength = 76;

    /// <summary>The share of a quadratic curve's control point that moves each cubic control point: two thirds.</summary>
    private const float TwoThirds = 2F / 3F;

    /// <summary>The largest colour channel as a float.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The format of every number written: up to four decimals.</summary>
    private const string NumberFormat = "0.####";

    /// <summary>The characters a number can take: a sign, up to 39 digits for the largest float, a point and four decimals.</summary>
    private const int NumberChars = 48;

    /// <summary>The operator that draws a straight line.</summary>
    private const string LineTo = "lineto";

    /// <summary>The operator that starts a contour.</summary>
    private const string MoveTo = "moveto";

    /// <summary>Writes a number as the shortest decimal text, with no exponent.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    internal static string Format(float value) =>
        float.IsFinite(value) ? value.ToString(NumberFormat, CultureInfo.InvariantCulture) : "0";

    /// <summary>Writes a line.</summary>
    /// <param name="text">The text.</param>
    internal void Line(string text)
    {
        output.Write(text);
        output.Write('\n');
    }

    /// <summary>Writes text and a space, so the next operand or operator follows it.</summary>
    /// <param name="text">The text.</param>
    internal void Word(string text)
    {
        output.Write(text);
        output.Write(' ');
    }

    /// <summary>Ends the line being written.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EndLine() => output.Write('\n');

    /// <summary>Writes a number followed by a space.</summary>
    /// <param name="value">The number.</param>
    internal void Number(float value)
    {
        Span<char> text = stackalloc char[NumberChars];
        if (float.IsFinite(value) && value.TryFormat(text, out var written, NumberFormat, CultureInfo.InvariantCulture))
        {
            output.Write(text[..written]);
        }
        else
        {
            output.Write('0');
        }

        output.Write(' ');
    }

    /// <summary>Writes a matrix as an array operand followed by <c>concat</c>.</summary>
    /// <param name="matrix">The matrix.</param>
    internal void Concat(Matrix3x2 matrix)
    {
        Matrix(matrix);
        Word("concat");
    }

    /// <summary>Writes a matrix as an array operand.</summary>
    /// <param name="matrix">The matrix.</param>
    internal void Matrix(Matrix3x2 matrix)
    {
        output.Write('[');
        Number(matrix.M11);
        Number(matrix.M12);
        Number(matrix.M21);
        Number(matrix.M22);
        Number(matrix.M31);
        Number(matrix.M32);
        output.Write("] ");
    }

    /// <summary>Sets the colour, lightened towards white by transparency because PostScript paints opaquely.</summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity from 0 to 1.</param>
    internal void Color(byte red, byte green, byte blue, float alpha)
    {
        Number(OverWhite(red, alpha));
        Number(OverWhite(green, alpha));
        Number(OverWhite(blue, alpha));
        Word("setrgbcolor");
    }

    /// <summary>Writes a path as a fresh current path.</summary>
    /// <param name="path">The path.</param>
    internal void Path(PdfPath path)
    {
        Word("newpath");
        var current = default(PdfPoint);
        foreach (var command in path.Commands)
        {
            current = WriteCommand(command, current);
        }
    }

    /// <summary>Writes a rectangle as a fresh current path.</summary>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The lower edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The upper edge.</param>
    internal void Rectangle(float left, float bottom, float right, float top)
    {
        Word("newpath");
        Number(left);
        Number(bottom);
        Word(MoveTo);
        Number(right);
        Number(bottom);
        Word(LineTo);
        Number(right);
        Number(top);
        Word(LineTo);
        Number(left);
        Number(top);
        Word(LineTo);
        Word("closepath");
    }

    /// <summary>Writes bytes as a hexadecimal string in short lines.</summary>
    /// <param name="bytes">The bytes.</param>
    internal void HexData(ReadOnlySpan<byte> bytes)
    {
        const int bytesPerLine = DataLineLength / 2;
        output.Write('<');
        while (!bytes.IsEmpty)
        {
            var take = Math.Min(bytesPerLine, bytes.Length);
            output.Write(Convert.ToHexString(bytes[..take]));
            bytes = bytes[take..];
            if (!bytes.IsEmpty)
            {
                output.Write('\n');
            }
        }

        output.Write('>');
        output.Write('\n');
    }

    /// <summary>Writes data as ASCII base-85 text of its zlib stream, in short lines and ending with the end-of-data marker.</summary>
    /// <param name="data">The data.</param>
    internal void CompressedData(ReadOnlySpan<byte> data)
    {
        var compressed = default(PooledBuffer);
        var armoured = default(PooledBuffer);
        try
        {
            ZLibCodec.Compress(data, PdfCompressionLevel.Balanced, ref compressed);
            Ascii85Filter.Encode(compressed.WrittenSpan, ref armoured);
            WriteLines(armoured.WrittenSpan);
        }
        finally
        {
            compressed.Dispose();
            armoured.Dispose();
        }
    }

    /// <summary>Moves a curve end two thirds of the way to a quadratic control point.</summary>
    /// <param name="end">The end of the curve.</param>
    /// <param name="control">The quadratic control point.</param>
    /// <returns>The cubic control point.</returns>
    private static PdfPoint Towards(PdfPoint end, PdfPoint control) =>
        new(end.X + ((control.X - end.X) * TwoThirds), end.Y + ((control.Y - end.Y) * TwoThirds));

    /// <summary>Lightens a channel towards white by transparency.</summary>
    /// <param name="channel">The channel from 0 to 255.</param>
    /// <param name="alpha">The opacity from 0 to 1.</param>
    /// <returns>The channel from 0 to 1.</returns>
    private static float OverWhite(byte channel, float alpha)
    {
        var opacity = Math.Clamp(alpha, 0, 1);
        return (ChannelMax - ((ChannelMax - channel) * opacity)) / ChannelMax;
    }

    /// <summary>Writes one path command and returns the point it ended at.</summary>
    /// <param name="command">The command.</param>
    /// <param name="current">The point before it.</param>
    /// <returns>The current point after it.</returns>
    private PdfPoint WriteCommand(in PdfPathCommand command, PdfPoint current)
    {
        switch (command.Kind)
        {
            case PdfPathCommandKind.MoveTo:
            {
                Point(command.Point);
                Word(MoveTo);
                return command.Point;
            }

            case PdfPathCommandKind.LineTo:
            {
                Point(command.Point);
                Word(LineTo);
                return command.Point;
            }

            case PdfPathCommandKind.QuadraticTo:
            {
                // PostScript has only cubic curves, so a quadratic's control point is split between two.
                Point(Towards(current, command.Control1));
                Point(Towards(command.Point, command.Control1));
                Point(command.Point);
                Word("curveto");
                return command.Point;
            }

            case PdfPathCommandKind.CubicTo:
            {
                Point(command.Control1);
                Point(command.Control2);
                Point(command.Point);
                Word("curveto");
                return command.Point;
            }

            default:
            {
                Word("closepath");
                return current;
            }
        }
    }

    /// <summary>Writes a point's two numbers.</summary>
    /// <param name="point">The point.</param>
    private void Point(PdfPoint point)
    {
        Number(point.X);
        Number(point.Y);
    }

    /// <summary>Writes ASCII bytes as text lines of at most <see cref="DataLineLength"/> characters.</summary>
    /// <param name="ascii">The bytes, which must be 7-bit characters.</param>
    private void WriteLines(ReadOnlySpan<byte> ascii)
    {
        Span<char> line = stackalloc char[DataLineLength];
        while (!ascii.IsEmpty)
        {
            var take = Math.Min(DataLineLength, ascii.Length);
            for (var i = 0; i < take; i++)
            {
                line[i] = (char)ascii[i];
            }

            output.Write(line[..take]);
            output.Write('\n');
            ascii = ascii[take..];
        }
    }
}
