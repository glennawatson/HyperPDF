// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Writes actual fractional group results separately from standards-oracle fixtures.</summary>
internal static class FractionalGroupArtifacts
{
    /// <summary>Retains the ideal float color and observed bytes without inferring conformance.</summary>
    /// <param name="pdf">The generated fractional fixture.</param>
    /// <param name="hyper">The actual HyperPDF raster.</param>
    /// <param name="pdfium">The actual PDFium raster.</param>
    /// <param name="pdfJs">The actual browser result.</param>
    /// <param name="decision">The explicitly unresolved classification.</param>
    /// <param name="cancellationToken">Cancels evidence writes.</param>
    /// <returns>A task.</returns>
    internal static async Task WriteAsync(
        byte[] pdf,
        ComparisonRaster hyper,
        ComparisonRaster pdfium,
        PdfJsRenderResult pdfJs,
        ComparisonDecision decision,
        CancellationToken cancellationToken)
    {
        const double half = 0.5;
        const string source = "https://opensource.adobe.com/dc-acrobat-sdk-docs/standards/pdfstandards/pdf/PDF32000_2008.pdf";
        const string clause = $"ISO 32000-1 11.3.4 Blending Colour Space, 11.4.4 Group Compositing Computations, 11.7.2 note 5 intermediate precision; {source}";
        var root = Environment.GetEnvironmentVariable("HYPERPDF_COMPARISON_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "pdfviewer-rendering-comparison");
        var directory = Path.Combine(root, $"FractionalGroup-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        // The standard defines component arithmetic but no one-byte bound on multi-step DeviceRGB raster rounding.
        var artifact = new FractionalGroupArtifact(
            Convert.ToHexString(SHA256.HashData(pdf)),
            RuntimeInformation.FrameworkDescription,
            clause,
            new(half, half, 0),
            new(hyper.Width, hyper.Height),
            new(pdfium.Width, pdfium.Height),
            new(pdfJs.Raster.Width, pdfJs.Raster.Height),
            Center(hyper),
            Center(pdfium),
            Center(pdfJs.Raster),
            decision,
            pdfJs.Metadata);
        await File.WriteAllBytesAsync(Path.Combine(directory, "fixture.pdf"), pdf, cancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(directory, "fractional-comparison.json"),
            JsonSerializer.SerializeToUtf8Bytes(artifact, ComparisonArtifactJson.Default.FractionalGroupArtifact),
            cancellationToken);
        await ComparisonArtifacts.WritePngAsync(Path.Combine(directory, "hyperpdf.png"), hyper, cancellationToken);
        await ComparisonArtifacts.WritePngAsync(Path.Combine(directory, "pdfium.png"), pdfium, cancellationToken);
        await ComparisonArtifacts.WritePngAsync(Path.Combine(directory, "pdfjs.png"), pdfJs.Raster, cancellationToken);
    }

    /// <summary>Reads an interior pixel using each renderer's actual geometry.</summary>
    /// <param name="raster">The owned raster.</param>
    /// <returns>The observed BGRA32 center pixel.</returns>
    private static Bgra32Color Center(ComparisonRaster raster)
    {
        const int halves = 2;
        var offset = ((raster.Height / halves * raster.Width) + (raster.Width / halves)) * ComparisonRaster.BytesPerPixel;
        var channels = raster.Pixels.Span.Slice(offset, ComparisonRaster.BytesPerPixel);
        return new(channels[0], channels[1], channels[2], channels[3]);
    }
}
