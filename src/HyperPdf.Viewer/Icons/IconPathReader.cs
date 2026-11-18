// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;

namespace HyperPdf.Viewer.Icons;

/// <summary>Reads the path data of an icon, as UTF-8, and strokes it. Only the absolute commands M, L, C, A and Z are read.</summary>
internal static class IconPathReader
{
    /// <summary>Strokes every line of an icon.</summary>
    /// <param name="path">The path data.</param>
    /// <param name="pen">The stroker.</param>
    internal static void Draw(ReadOnlySpan<byte> path, ref IconStroker pen)
    {
        var rest = path;
        while (!rest.IsEmpty)
        {
            var command = rest[0];
            rest = rest[1..];
            switch (command)
            {
                case (byte)'M':
                    {
                        var to = Point(ref rest);
                        pen.MoveTo(to.X, to.Y);
                        break;
                    }

                case (byte)'L':
                    {
                        var to = Point(ref rest);
                        pen.LineTo(to.X, to.Y);
                        break;
                    }

                case (byte)'C':
                    {
                        var first = Point(ref rest);
                        var second = Point(ref rest);
                        pen.CubicTo(first, second, Point(ref rest));
                        break;
                    }

                case (byte)'A':
                    {
                        Arc(ref rest, ref pen);
                        break;
                    }

                case (byte)'Z':
                    {
                        pen.Close();
                        break;
                    }

                default:
                    {
                        // Spaces, commas and line ends between commands carry nothing.
                        break;
                    }
            }
        }
    }

    /// <summary>Reads an arc and strokes it. The turn of the ellipse is read and left out, because every icon's arcs are upright.</summary>
    /// <param name="rest">The path data from the arc's numbers on, moved past them.</param>
    /// <param name="pen">The stroker.</param>
    private static void Arc(ref ReadOnlySpan<byte> rest, ref IconStroker pen)
    {
        var radii = Point(ref rest);
        _ = Number(ref rest);
        var large = Number(ref rest) != 0F;
        var sweep = Number(ref rest) != 0F;
        pen.ArcTo(radii, large, sweep, Point(ref rest));
    }

    /// <summary>Reads two numbers as a point.</summary>
    /// <param name="rest">The path data, moved past the numbers.</param>
    /// <returns>The point.</returns>
    private static IconPoint Point(ref ReadOnlySpan<byte> rest)
    {
        var x = Number(ref rest);
        return new(x, Number(ref rest));
    }

    /// <summary>Reads one number, after any spaces and commas before it.</summary>
    /// <param name="rest">The path data, moved past the number.</param>
    /// <returns>The number, or zero when there is none.</returns>
    private static float Number(ref ReadOnlySpan<byte> rest)
    {
        while (!rest.IsEmpty && rest[0] is (byte)' ' or (byte)',')
        {
            rest = rest[1..];
        }

        if (!Utf8Parser.TryParse(rest, out float value, out var used))
        {
            return 0F;
        }

        rest = rest[used..];
        return value;
    }
}
