// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace HyperPdf.Examples;

/// <summary>Command-line entry point for the end-to-end HyperPDF example.</summary>
public static partial class Program
{
    /// <summary>Runs the end-to-end document creation and rendering example.</summary>
    /// <param name="args">Optional output directory path where <c>example.pdf</c> and <c>page-1.png</c> will be saved.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var logging = LoggerFactory.Create(static builder => builder.AddSimpleConsole());
        var logger = logging.CreateLogger("HyperPdf.Examples");
        var report = await EndToEndExample.RunAsync(CancellationToken.None).ConfigureAwait(false);
        LogCreated(logger, report.PdfBytes.Length, report.PageCount);
        LogRendered(logger, report.RenderedWidth, report.RenderedHeight, report.FirstPagePngBytes.Length);
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            return;
        }

        var outputDirectory = Path.GetFullPath(args[0]);
        _ = Directory.CreateDirectory(outputDirectory);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "example.pdf"), report.PdfBytes).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "page-1.png"), report.FirstPagePngBytes).ConfigureAwait(false);
    }

    /// <summary>Logs that the document was created.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="bytes">The size of the PDF in bytes.</param>
    /// <param name="pages">The pages in the PDF.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Created PDF ({Bytes} bytes, {Pages} page(s)).")]
    private static partial void LogCreated(ILogger logger, int bytes, int pages);

    /// <summary>Logs that the first page was rendered.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="width">The width of the picture in pixels.</param>
    /// <param name="height">The height of the picture in pixels.</param>
    /// <param name="bytes">The size of the PNG in bytes.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Rendered page 1 to PNG ({Width}x{Height} px, {Bytes} bytes).")]
    private static partial void LogRendered(ILogger logger, int width, int height, int bytes);
}
