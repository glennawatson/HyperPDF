// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Builds the transparency-heavy and annotation pages the render benchmarks draw.</summary>
internal static class RenderSamplePages
{
    /// <summary>The annotation rows on the annotation page.</summary>
    private const int AnnotationRows = 6;

    /// <summary>The vertical distance between annotation rows, in points.</summary>
    private const int RowHeight = 120;

    /// <summary>The height of each annotation, in points.</summary>
    private const int AnnotationHeight = 80;

    /// <summary>Half the height of each annotation, in points.</summary>
    private const int HalfHeight = 40;

    /// <summary>The bottom of the first annotation row, in points.</summary>
    private const int FirstRow = 40;

    /// <summary>The page objects shared by both samples: catalog, page tree and page, which refers to objects 4 (content) and 5 (resources).</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>
    /// Creates a Letter page of overlapping translucent, blended, knockout and soft-masked groups over a gradient, with a
    /// translucent fill and stroke pair and translucent text-free shapes.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] CreateTransparencyPage()
    {
        const string content =
            "/Sh sh /Half gs /Knock Do q 1 0 0 1 200 0 cm /Iso Do Q q 1 0 0 1 0 250 cm /Mul gs /Iso Do Q "
            + "/Masked gs 1 0 0 rg 300 300 250 250 re f /Half gs 0 0 1 RG 12 w 0 0.6 0 rg 60 560 200 160 re B";
        const string group = "1 0 0 rg 40 40 200 200 re f 0 0 1 rg 140 140 200 200 re f 0 1 0 rg 90 200 120 120 re f";
        return MiniPdf.Build(
            Catalog,
            Pages,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content),
            "<< /ExtGState << /Half << /ca 0.5 /CA 0.5 >> /Mul << /BM /Multiply /ca 0.8 >> /Masked << /SMask << /S /Luminosity /G 8 0 R /BC [0.2] >> >> >> "
            + "/XObject << /Knock 6 0 R /Iso 7 0 R >> "
            + "/Shading << /Sh << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 612 792] "
            + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 1 0.8] /C1 [0.6 0.8 1] /N 1 >> /Extend [true true] >> >> >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 400 400] /Group << /S /Transparency /I true /K true >>", group),
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 400 400] /Group << /S /Transparency /I false >>", group),
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 612 792] /Group << /S /Transparency /CS /DeviceGray >>", "1 g 300 300 120 250 re f 0.5 g 420 300 130 250 re f"));
    }

    /// <summary>Creates a dense, searchable text page using a standard PDF font.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] CreateTextPage()
    {
        const int Lines = 56;
        const int FirstBaseline = 750;
        const int LineSpacing = 12;
        var content = new StringBuilder();
        for (var line = 0; line < Lines; line++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"BT /F1 10 Tf 1 0 0 1 36 {FirstBaseline - (line * LineSpacing)} Tm (Flight chart reading order and visible text line {line}) Tj ET\n");
        }

        return MiniPdf.Build(
            Catalog,
            Pages,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
    }

    /// <summary>Creates a deterministic image-only PDF with a varied greyscale scan.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] CreateScanPage()
    {
        const int ScanEdge = 1024;
        var pixels = new byte[ScanEdge * ScanEdge];
        var value = 0x5EEDU;
        for (var i = 0; i < pixels.Length; i++)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            pixels[i] = (byte)value;
        }

        return TestPdf.CreateScan(pixels, ScanEdge, ScanEdge);
    }

    /// <summary>Creates a Letter page of annotations with no appearance streams, so every one is generated.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] CreateAnnotationPage()
    {
        var objects = new List<string> { Catalog, Pages, string.Empty, MiniPdf.Stream(string.Empty, "0.9 g 0 0 612 792 re f"), "<< >>" };
        var references = new StringBuilder();
        for (var row = 0; row < AnnotationRows; row++)
        {
            var bottom = FirstRow + (row * RowHeight);
            foreach (var annotation in RowAnnotations(bottom))
            {
                objects.Add(annotation);
                _ = references.Append(CultureInfo.InvariantCulture, $"{objects.Count} 0 R ");
            }
        }

        objects[2] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R /Annots [{references}] >>";
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Gets one row of annotations of every generated kind.</summary>
    /// <param name="y">The bottom of the row.</param>
    /// <returns>The annotation dictionaries.</returns>
    private static string[] RowAnnotations(int y)
    {
        var top = y + AnnotationHeight;
        var middle = y + HalfHeight;
        return
        [
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Square /Rect [20 {y} 100 {top}] /C [0 0 1] /IC [1 0.8 0.8] /BS << /W 3 >> /F 4 >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Circle /Rect [110 {y} 190 {top}] /C [1 0 0] /CA 0.6 /F 4 >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Ink /Rect [200 {y} 280 {top}] /InkList [[205 {y} 240 {top} 275 {y}]] /C [0 0.5 0] /BS << /W 2 >> /F 4 >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Highlight /Rect [290 {y} 400 {middle}] /QuadPoints [290 {middle} 400 {middle} 290 {y} 400 {y}] /F 4 >>"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Annot /Subtype /Squiggly /Rect [290 {middle} 400 {top}] /QuadPoints [290 {top} 400 {top} 290 {middle} 400 {middle}] /F 4 >>"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Annot /Subtype /Polygon /Rect [410 {y} 500 {top}] /Vertices [410 {y} 500 {y} 455 {top}] /IC [0.8 1 0.8] /BE << /S /C /I 1 >> /F 4 >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Line /Rect [510 {y} 590 {top}] /L [510 {y} 590 {top}] /LE [/OpenArrow /ClosedArrow] /F 4 >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Text /Rect [560 {middle} 580 {top}] /F 4 >>"),
        ];
    }
}
