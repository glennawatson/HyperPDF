// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace HyperPdf.Viewer.Generator;

/// <summary>
/// Reads one icon from an SVG file. The file's <c>title</c> says what the icon shows, the <c>data-tone</c> attribute of its root
/// names the kind of action it stands for, and its paths and simple shapes make the outline, drawn on a 24 by 24 grid.
/// </summary>
internal static class IconReader
{
    /// <summary>The tone of an icon whose file names none.</summary>
    internal const string PlainTone = "plain";

    /// <summary>Reads an icon.</summary>
    /// <param name="name">The icon's name: the file's name without its extension.</param>
    /// <param name="svg">The file's text.</param>
    /// <returns>The icon, with what is wrong with the file when it cannot be read.</returns>
    internal static IconModel Read(string name, string svg)
    {
        try
        {
            return ReadCore(name, svg);
        }
        catch (XmlException problem)
        {
            return new(name, string.Empty, PlainTone, string.Empty, problem.Message);
        }
        catch (FormatException problem)
        {
            return new(name, string.Empty, PlainTone, string.Empty, problem.Message);
        }
    }

    /// <summary>Reads an icon from a file that is taken to be well formed.</summary>
    /// <param name="name">The icon's name.</param>
    /// <param name="svg">The file's text.</param>
    /// <returns>The icon.</returns>
    private static IconModel ReadCore(string name, string svg)
    {
        using var text = new StringReader(svg);
        using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var path = new StringBuilder();
        var outline = new PathNormalizer(path);
        var summary = string.Empty;
        var tone = PlainTone;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "svg":
                    {
                        tone = reader.GetAttribute("data-tone") ?? PlainTone;
                        break;
                    }

                case "title":
                    {
                        summary = TextOf(reader);
                        break;
                    }

                default:
                    {
                        AppendShape(reader, outline);
                        break;
                    }
            }
        }

        return new(name, summary, tone, path.ToString().TrimEnd(), Problem(summary, path.Length));
    }

    /// <summary>Says what a well formed file still lacks.</summary>
    /// <param name="summary">The file's title.</param>
    /// <param name="pathLength">The length of the outline read.</param>
    /// <returns>What is missing, or empty.</returns>
    private static string Problem(string summary, int pathLength)
    {
        if (summary.Length == 0)
        {
            return "the file needs a <title> that says what the icon shows";
        }

        return pathLength == 0 ? "the file has no path or shape to draw" : string.Empty;
    }

    /// <summary>Reads the text inside the element the reader is on.</summary>
    /// <param name="reader">The reader, on the element's start.</param>
    /// <returns>The text, or empty.</returns>
    private static string TextOf(XmlReader reader) =>
        !reader.IsEmptyElement && reader.Read() && reader.NodeType == XmlNodeType.Text ? reader.Value.Trim() : string.Empty;

    /// <summary>Adds the outline of a path or a simple shape. Any other element, such as a group, adds nothing.</summary>
    /// <param name="reader">The reader, on the element's start.</param>
    /// <param name="outline">Receives the outline.</param>
    private static void AppendShape(XmlReader reader, PathNormalizer outline)
    {
        switch (reader.LocalName)
        {
            case "path":
                {
                    outline.Append(reader.GetAttribute("d") ?? string.Empty);
                    break;
                }

            case "line":
                {
                    outline.MoveTo(new(Number(reader, "x1"), Number(reader, "y1")));
                    outline.LineTo(new(Number(reader, "x2"), Number(reader, "y2")));
                    break;
                }

            case "polyline" or "polygon":
                {
                    AppendPoints(reader.GetAttribute("points") ?? string.Empty, outline, reader.LocalName == "polygon");
                    break;
                }

            default:
                {
                    AppendRound(reader, outline);
                    break;
                }
        }
    }

    /// <summary>Adds the outline of a rectangle, a circle or an ellipse.</summary>
    /// <param name="reader">The reader, on the element's start.</param>
    /// <param name="outline">Receives the outline.</param>
    private static void AppendRound(XmlReader reader, PathNormalizer outline)
    {
        switch (reader.LocalName)
        {
            case "rect":
                {
                    var corner = new SvgPoint(Number(reader, "x"), Number(reader, "y"));
                    var far = new SvgPoint(corner.X + Number(reader, "width"), corner.Y + Number(reader, "height"));
                    outline.MoveTo(corner);
                    outline.LineTo(new(far.X, corner.Y));
                    outline.LineTo(far);
                    outline.LineTo(new(corner.X, far.Y));
                    outline.Close();
                    break;
                }

            case "circle":
                {
                    var radius = Number(reader, "r");
                    AppendEllipse(outline, new(Number(reader, "cx"), Number(reader, "cy")), new(radius, radius));
                    break;
                }

            case "ellipse":
                {
                    AppendEllipse(outline, new(Number(reader, "cx"), Number(reader, "cy")), new(Number(reader, "rx"), Number(reader, "ry")));
                    break;
                }

            default:
                {
                    // Groups, definitions and the like carry no outline of their own.
                    break;
                }
        }
    }

    /// <summary>Adds an ellipse as two half arcs.</summary>
    /// <param name="outline">Receives the outline.</param>
    /// <param name="centre">The middle of the ellipse.</param>
    /// <param name="radii">Its radii.</param>
    private static void AppendEllipse(PathNormalizer outline, SvgPoint centre, SvgPoint radii)
    {
        var flags = new SvgPoint(1, 1);
        outline.MoveTo(new(centre.X + radii.X, centre.Y));
        outline.ArcTo(radii, 0, flags, new(centre.X - radii.X, centre.Y));
        outline.ArcTo(radii, 0, flags, new(centre.X + radii.X, centre.Y));
        outline.Close();
    }

    /// <summary>Adds the lines between the points of a polyline or a polygon.</summary>
    /// <param name="points">The points, as pairs of numbers.</param>
    /// <param name="outline">Receives the outline.</param>
    /// <param name="closed">Whether the last point joins the first.</param>
    private static void AppendPoints(string points, PathNormalizer outline, bool closed)
    {
        var scanner = new PathScanner(points);
        var first = true;
        while (scanner.HasNumber())
        {
            var point = new SvgPoint(scanner.ReadNumber(), scanner.ReadNumber());
            if (first)
            {
                outline.MoveTo(point);
                first = false;
            }
            else
            {
                outline.LineTo(point);
            }
        }

        if (closed && !first)
        {
            outline.Close();
        }
    }

    /// <summary>Reads a number from an attribute.</summary>
    /// <param name="reader">The reader, on the element's start.</param>
    /// <param name="attribute">The attribute's name.</param>
    /// <returns>The number, or zero when the attribute is not there.</returns>
    /// <exception cref="FormatException">The attribute is not a number.</exception>
    private static double Number(XmlReader reader, string attribute)
    {
        if (reader.GetAttribute(attribute) is not { Length: > 0 } text)
        {
            return 0;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"the attribute {attribute}=\"{text}\" is not a plain number");
    }
}
