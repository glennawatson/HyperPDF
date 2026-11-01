// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Services;

namespace HyperPdf.ViewModels.Output;

/// <summary>Turns picture files into a new PDF 2.0 file with a page for each picture.</summary>
public static class PictureConversion
{
    /// <summary>The file name extension of the new PDF.</summary>
    private const string PdfExtension = ".pdf";

    /// <summary>Gets the name suggested for the new PDF: the first picture's name with the PDF extension.</summary>
    /// <param name="paths">The picture files.</param>
    /// <returns>The file name.</returns>
    public static string SuggestName(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return (paths.Count == 0 ? "Pictures" : Path.GetFileNameWithoutExtension(paths[0])) + PdfExtension;
    }

    /// <summary>Reads the pictures and writes them as a PDF.</summary>
    /// <param name="services">The app services, whose engine writes the PDF and whose render head reads the pictures.</param>
    /// <param name="paths">The picture files, in page order.</param>
    /// <param name="destination">The PDF file to write.</param>
    /// <param name="cancellationToken">Cancels reading and writing.</param>
    /// <returns>The number of pages written, which is 0 when no file could be read as a picture or the engine cannot write PDFs from pictures.</returns>
    public static async Task<int> ConvertAsync(AppServices services, IReadOnlyList<string> paths, string destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (services.Engine is not IPictureDocumentWriter writer)
        {
            return 0;
        }

        var pictures = await Task.Run(() => Read(services, paths, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (pictures.Length == 0)
        {
            return 0;
        }

        // The file is written beside its destination first, so a failure never leaves half a PDF where one was asked for.
        var temporary = $"{destination}.writing";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                await writer.WritePicturesAsync(pictures, stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, destination, true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return pictures.Length;
    }

    /// <summary>Reads each picture in order, skipping files that are not pictures.</summary>
    /// <param name="services">The app services.</param>
    /// <param name="paths">The picture files.</param>
    /// <param name="cancellationToken">Stops reading between files.</param>
    /// <returns>The pictures that were read.</returns>
    private static PageImageSource[] Read(AppServices services, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var pictures = new List<PageImageSource>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (services.Head.LoadImage(path) is { } decoded)
            {
                pictures.Add(new(decoded.Pixels, decoded.Width, decoded.Height));
            }
        }

        return [.. pictures];
    }
}
