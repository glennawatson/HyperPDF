// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Writes structured comparison evidence outside the repository before any result assertion.</summary>
internal static class ComparisonArtifacts
{
    /// <summary>Retains source PDF, scores and metadata, plus PNGs when any renderer violates the oracle.</summary>
    /// <param name="kind">The graphics fixture.</param>
    /// <param name="fixture">The PDF and independent expectations.</param>
    /// <param name="hyper">The HyperPDF raster.</param>
    /// <param name="pdfium">The PDFium raster.</param>
    /// <param name="pdfJs">The actual browser result.</param>
    /// <param name="decision">The independently scored reference decision.</param>
    /// <param name="cancellationToken">Cancels evidence writes.</param>
    /// <returns>A task.</returns>
    internal static async Task WriteAsync(
        StandardsRenderCase kind,
        StandardsRenderFixture fixture,
        ComparisonRaster hyper,
        ComparisonRaster pdfium,
        PdfJsRenderResult pdfJs,
        ComparisonDecision decision,
        CancellationToken cancellationToken)
    {
        var root = Environment.GetEnvironmentVariable("HYPERPDF_COMPARISON_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "pdfviewer-rendering-comparison");
        var directory = Path.Combine(root, $"{kind}-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var artifact = new ComparisonArtifact(
            kind,
            Convert.ToHexString(SHA256.HashData(fixture.Pdf)),
            RuntimeInformation.FrameworkDescription,
            fixture.Oracle.Width,
            fixture.Oracle.Height,
            new(hyper.Width, hyper.Height),
            new(pdfium.Width, pdfium.Height),
            new(pdfJs.Raster.Width, pdfJs.Raster.Height),
            fixture.Oracle.Regions.ToArray(),
            decision,
            RasterScoring.Compare(hyper, pdfium, 0),
            RasterScoring.Compare(hyper, pdfJs.Raster, 0),
            RasterScoring.Compare(pdfium, pdfJs.Raster, 0),
            pdfJs.Metadata);
        await File.WriteAllBytesAsync(Path.Combine(directory, "fixture.pdf"), fixture.Pdf, cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "comparison.json"), JsonSerializer.SerializeToUtf8Bytes(artifact, ComparisonArtifactJson.Default.ComparisonArtifact), cancellationToken);
        if (decision.HyperPdfScore is not { IsAcceptable: false } && decision.PdfiumScore is not { IsAcceptable: false } && decision.PdfJsScore is not { IsAcceptable: false })
        {
            return;
        }

        await WritePngAsync(Path.Combine(directory, "hyperpdf.png"), hyper, cancellationToken);
        await WritePngAsync(Path.Combine(directory, "pdfium.png"), pdfium, cancellationToken);
        await WritePngAsync(Path.Combine(directory, "pdfjs.png"), pdfJs.Raster, cancellationToken);
    }

    /// <summary>Encodes the exact BGRA32 output for a failed independent check.</summary>
    /// <param name="path">The output path.</param>
    /// <param name="raster">The actual pixels.</param>
    /// <param name="cancellationToken">Cancels the file write.</param>
    /// <returns>A task.</returns>
    internal static async Task WritePngAsync(string path, ComparisonRaster raster, CancellationToken cancellationToken)
    {
        const int quality = 100;
        using var image = SKImage.FromPixelCopy(new(raster.Width, raster.Height, SKColorType.Bgra8888, SKAlphaType.Premul), raster.Pixels.ToArray(), raster.Width * ComparisonRaster.BytesPerPixel);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality);
        await File.WriteAllBytesAsync(path, data.ToArray(), cancellationToken);
    }
}
