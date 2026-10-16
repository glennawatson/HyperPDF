// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Examples;

/// <summary>Command-line entry point for the end-to-end HyperPDF example.</summary>
public static class Program
{
    /// <summary>Runs the end-to-end document creation and rendering example.</summary>
    /// <param name="args">Optional output directory path where <c>example.pdf</c> and <c>page-1.png</c> will be saved.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var report = await EndToEndExample.RunAsync(CancellationToken.None).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Created PDF ({report.PdfBytes.Length} bytes, {report.PageCount} page(s)).").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Rendered page 1 to PNG ({report.RenderedWidth}x{report.RenderedHeight} px, {report.FirstPagePngBytes.Length} bytes).").ConfigureAwait(false);
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            return;
        }

        var outputDirectory = Path.GetFullPath(args[0]);
        _ = Directory.CreateDirectory(outputDirectory);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "example.pdf"), report.PdfBytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "page-1.png"), report.FirstPagePngBytes).ConfigureAwait(false);
    }
}
