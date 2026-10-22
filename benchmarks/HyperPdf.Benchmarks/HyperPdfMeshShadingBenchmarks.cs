// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Renders a page whose only content is a free-form triangle mesh (type 4 shading) of 2,000 triangles.</summary>
public class HyperPdfMeshShadingBenchmarks
{
    /// <summary>The triangles in the mesh.</summary>
    private const int Triangles = 2000;

    /// <summary>The vertices of a triangle.</summary>
    private const int Corners = 3;

    /// <summary>The values a byte holds, as a divisor to spread positions across the page.</summary>
    private const int ByteValues = 256;

    /// <summary>The step that spreads vertex x positions.</summary>
    private const int StrideX = 7;

    /// <summary>The step that spreads vertex y positions.</summary>
    private const int StrideY = 13;

    /// <summary>The step that spreads the red component.</summary>
    private const int StrideRed = 3;

    /// <summary>The step that spreads the green component.</summary>
    private const int StrideGreen = 5;

    /// <summary>The step that spreads the blue component.</summary>
    private const int StrideBlue = 11;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>Builds the document.</summary>
    [GlobalSetup]
    public void Setup() => _pdf = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Shading << /Sh 5 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, "/Sh sh"),
        MiniPdf.Stream("/ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 612 0 792 0 1 0 1 0 1] /Filter /ASCIIHexDecode", Mesh()));

    /// <summary>Opens the document and records and draws the page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderMesh()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }

    /// <summary>Writes the vertices as hexadecimal: a flag of zero, a position and a colour for each.</summary>
    /// <returns>The hexadecimal text.</returns>
    private static string Mesh()
    {
        var hex = new StringBuilder();
        for (var i = 0; i < Triangles; i++)
        {
            for (var corner = 0; corner < Corners; corner++)
            {
                var n = (i * Corners) + corner;
                _ = hex.Append(CultureInfo.InvariantCulture, $"00{n * StrideX % ByteValues:X2}{n * StrideY % ByteValues:X2}");
                _ = hex.Append(CultureInfo.InvariantCulture, $"{n * StrideRed % ByteValues:X2}{n * StrideGreen % ByteValues:X2}{n * StrideBlue % ByteValues:X2}");
            }
        }

        return hex.Append('>').ToString();
    }
}
