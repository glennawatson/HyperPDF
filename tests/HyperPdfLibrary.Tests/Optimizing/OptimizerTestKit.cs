// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Runs the optimiser and compares documents before and after.</summary>
internal static class OptimizerTestKit
{
    /// <summary>The bytes of a rendered pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The colour channels compared in a pixel: blue, green and red.</summary>
    private const int ColorChannels = 3;

    /// <summary>Optimises a document held in memory.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="options">The options.</param>
    /// <returns>The output and report.</returns>
    internal static OptimizedFile Optimize(byte[] pdf, in PdfOptimizeOptions options)
    {
        using var document = SkiaTestRegistration.Open(pdf, null);
        using var output = new MemoryStream();
        var report = PdfOptimizer.Optimize(document, output, options);
        return new(output.ToArray(), report);
    }

    /// <summary>Renders a document's first page at one pixel per point.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The pixels.</returns>
    internal static RenderedImage Render(byte[] pdf)
    {
        using var page = new RenderTestPage(pdf);
        return page.RenderPage(1, 0, PdfRenderFlags.None);
    }

    /// <summary>Gets the mean difference between two renders, per colour channel, from 0 to 255.</summary>
    /// <param name="before">The first render.</param>
    /// <param name="after">The second render, the same size.</param>
    /// <returns>The mean absolute difference.</returns>
    internal static double MeanDifference(RenderedImage before, RenderedImage after)
    {
        long total = 0;
        var samples = 0L;
        for (var i = 0; i + PixelBytes <= before.Pixels.Length && i + PixelBytes <= after.Pixels.Length; i += PixelBytes)
        {
            for (var c = 0; c < ColorChannels; c++)
            {
                total += Math.Abs(before.Pixels[i + c] - after.Pixels[i + c]);
                samples++;
            }
        }

        return samples == 0 ? 0 : (double)total / samples;
    }

    /// <summary>Gets the largest difference between two renders in any colour channel.</summary>
    /// <param name="before">The first render.</param>
    /// <param name="after">The second render, the same size.</param>
    /// <returns>The largest absolute difference.</returns>
    internal static int MaxDifference(RenderedImage before, RenderedImage after)
    {
        var largest = 0;
        for (var i = 0; i + PixelBytes <= before.Pixels.Length && i + PixelBytes <= after.Pixels.Length; i += PixelBytes)
        {
            for (var c = 0; c < ColorChannels; c++)
            {
                largest = Math.Max(largest, Math.Abs(before.Pixels[i + c] - after.Pixels[i + c]));
            }
        }

        return largest;
    }

    /// <summary>Extracts every page's text.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The text of each page.</returns>
    internal static string[] Text(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var pages = new string[document.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = PdfDocumentText.GetTextPage(document, i).Text;
        }

        return pages;
    }

    /// <summary>Writes bytes as a Latin-1 string, so binary data can go into a <see cref="HyperPdf.TestAssets.MiniPdf"/> object.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>One character per byte.</returns>
    internal static string Latin1(byte[] data) => Encoding.Latin1.GetString(data);

    /// <summary>Compresses bytes as zlib at the fastest level, so the optimiser has something to improve.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The compressed bytes.</returns>
    internal static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
