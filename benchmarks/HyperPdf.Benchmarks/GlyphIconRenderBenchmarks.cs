// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures warm tile rasterization of Type 3 bitmap glyphs drawn from cached masks, and of generated note and attachment icons.</summary>
public class GlyphIconRenderBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The lines of bitmap glyphs on the Type 3 page.</summary>
    private const int GlyphLines = 40;

    /// <summary>The distance between lines of bitmap glyphs, in points.</summary>
    private const int LineHeight = 14;

    /// <summary>The height of the first line of bitmap glyphs, in points.</summary>
    private const int FirstLine = 760;

    /// <summary>The left of the first icon, in points.</summary>
    private const int IconLeft = 20;

    /// <summary>The distance between icons, in points.</summary>
    private const int IconStep = 40;

    /// <summary>The side of an icon's rectangle, in points.</summary>
    private const int IconSize = 20;

    /// <summary>The bottom of the row of note icons, in points.</summary>
    private const int NoteRow = 700;

    /// <summary>The bottom of the row of attachment icons, in points.</summary>
    private const int AttachmentRow = 650;

    /// <summary>The names of the note icons.</summary>
    private static readonly string[] NoteNames = ["Comment", "Note", "Key", "Help", "Paragraph", "NewParagraph", "Insert"];

    /// <summary>The names of the attachment icons.</summary>
    private static readonly string[] AttachmentNames = ["PushPin", "Graph", "Paperclip", "Tag"];

    /// <summary>The tile at the page origin.</summary>
    private readonly PdfTileRequest _origin = new(0, 1, 0, 0, 0, PdfRenderFlags.Annotations);

    /// <summary>The reused caller-owned CPU pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The open document retained for warm replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer retaining recorded page pictures.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Gets or sets the workload measured.</summary>
    [Params("Type3Bitmap", "Type3Bitmap+Lcd", "Icons")]
    public string Workload { get; set; } = string.Empty;

    /// <summary>Opens and records the page, so masks and icons are made before measuring.</summary>
    /// <exception cref="InvalidOperationException">The page did not render.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = Workload == "Icons" ? CreateIconPage() : CreateType3Page();
        var quality = Workload switch
        {
            "Type3Bitmap" => PdfRenderQuality.Default with { GlyphMasks = true },
            "Type3Bitmap+Lcd" => PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Lcd, TextHinting = PdfTextHinting.Slight },
            _ => PdfRenderQuality.Default,
        };
        _document = BenchmarkDrawing.Open(bytes, null, _drawingServices);
        _renderer = new(_document, PdfRenderOptions.Default with { Quality = quality });
        if (!WarmReplay())
        {
            throw new InvalidOperationException($"The {Workload} page did not render.");
        }
    }

    /// <summary>Disposes the retained page recording and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Measures rasterization of the retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmReplay() => _renderer.Render(_origin, new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel));

    /// <summary>Makes a page of lines of bitmap glyphs from a Type 3 font whose one glyph is a stencil image mask.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateType3Page()
    {
        var content = new StringBuilder("1 0 0 rg BT /F1 10 Tf ");
        for (var line = 0; line < GlyphLines; line++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"1 0 0 1 10 {FirstLine - (line * LineHeight)} Tm (AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA) Tj ");
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a 6 0 R >> "
            + "/Encoding << /Differences [65 /a] >> /FirstChar 65 /LastChar 65 /Widths [1000] /Resources << /XObject << /Im 7 0 R >> >> >>",
            MiniPdf.Stream(string.Empty, "1000 0 0 0 1000 1000 d1 q 1000 0 0 1000 0 0 cm /Im Do Q"),
            MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 8 /Height 8 /ImageMask true /BitsPerComponent 1 /Filter /ASCIIHexDecode", "FF818181818181FF>"));
    }

    /// <summary>Makes a page with a note and an attachment annotation of every icon.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateIconPage()
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", string.Empty, MiniPdf.Stream(string.Empty, "1 g 0 0 612 792 re f") };
        var references = new StringBuilder();
        var column = 0;
        foreach (var name in NoteNames)
        {
            var left = IconLeft + (column * IconStep);
            objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Text /Name /{name} /Rect [{left} {NoteRow} {left + IconSize} {NoteRow + IconSize}] >>"));
            _ = references.Append(CultureInfo.InvariantCulture, $"{objects.Count} 0 R ");
            column++;
        }

        column = 0;
        foreach (var name in AttachmentNames)
        {
            var left = IconLeft + (column * IconStep);
            var rect = string.Create(CultureInfo.InvariantCulture, $"[{left} {AttachmentRow} {left + IconSize} {AttachmentRow + IconSize}]");
            objects.Add($"<< /Type /Annot /Subtype /FileAttachment /Name /{name} /Rect {rect} >>");
            _ = references.Append(CultureInfo.InvariantCulture, $"{objects.Count} 0 R ");
            column++;
        }

        objects[2] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Annots [{references}] >>";
        return MiniPdf.Build([.. objects]);
    }
}
