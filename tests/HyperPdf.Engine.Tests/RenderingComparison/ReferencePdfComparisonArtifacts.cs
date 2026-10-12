// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using SkiaSharp;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Writes reference PDF scores and three rendered page images outside the repository.</summary>
internal static class ReferencePdfComparisonArtifacts
{
    /// <summary>Retains every unreviewed page for independent visual review.</summary>
    /// <param name="artifact">The source identity, classifications and raw differences.</param>
    /// <param name="pdf">The exact rendered source bytes.</param>
    /// <param name="hyperPdf">The HyperPDF pixels.</param>
    /// <param name="pdfium">The PDFium pixels.</param>
    /// <param name="pdfJs">The pdf.js pixels.</param>
    /// <param name="cancellationToken">Cancels evidence writes.</param>
    /// <returns>The external artifact directory.</returns>
    internal static async Task<string> WriteAsync(
        ReferencePdfComparisonArtifact artifact,
        byte[] pdf,
        ComparisonRaster hyperPdf,
        ComparisonRaster pdfium,
        ComparisonRaster pdfJs,
        CancellationToken cancellationToken)
    {
        var root = Environment.GetEnvironmentVariable("HYPERPDF_COMPARISON_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "pdfviewer-rendering-comparison");
        var directory = Path.Combine(root, $"reference-pdf-{Path.GetFileNameWithoutExtension(artifact.PdfFile)}-{artifact.PageIndex}-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var attribution = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "reference-pdfs-attribution.md"), cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "ATTRIBUTION.md"), attribution, cancellationToken);
        var manifest = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "reference-pdfs-manifest.json"), cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "manifest.json"), manifest, cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "source.pdf"), pdf, cancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(directory, "comparison.json"),
            JsonSerializer.SerializeToUtf8Bytes(artifact, ReferencePdfComparisonJsonContext.Default.ReferencePdfComparisonArtifact),
            cancellationToken);
        await WritePngAsync(Path.Combine(directory, "hyperpdf.png"), hyperPdf, cancellationToken);
        await WritePngAsync(Path.Combine(directory, "pdfium.png"), pdfium, cancellationToken);
        await WritePngAsync(Path.Combine(directory, "pdfjs.png"), pdfJs, cancellationToken);
        return directory;
    }

    /// <summary>Reports raw similarity only when pixel geometry matches.</summary>
    /// <param name="left">The first renderer's pixels.</param>
    /// <param name="right">The second renderer's pixels.</param>
    /// <returns>The counts and nullable mean channel error.</returns>
    internal static RenderedPageDifference Compare(ComparisonRaster left, ComparisonRaster right)
    {
        var score = RasterScoring.Compare(left, right, 0);
        var mean = score.DimensionsMatch && score.CheckedPixels > 0 ? (double?)score.TotalChannelError / (score.CheckedPixels * ComparisonRaster.BytesPerPixel) : null;
        return new(score, mean);
    }

    /// <summary>Encodes the exact white-background BGRA32 snapshot.</summary>
    /// <param name="path">The external output path.</param>
    /// <param name="raster">The actual renderer pixels.</param>
    /// <param name="cancellationToken">Cancels file writing.</param>
    /// <returns>A task.</returns>
    private static async Task WritePngAsync(string path, ComparisonRaster raster, CancellationToken cancellationToken)
    {
        const int quality = 100;
        using var image = SKImage.FromPixelCopy(new(raster.Width, raster.Height, SKColorType.Bgra8888, SKAlphaType.Premul), raster.Pixels.ToArray(), raster.Width * ComparisonRaster.BytesPerPixel);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality);
        await File.WriteAllBytesAsync(path, data.ToArray(), cancellationToken);
    }
}
