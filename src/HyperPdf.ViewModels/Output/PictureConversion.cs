// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    /// <returns>
    /// A cold signal giving the number of pages written, which is 0 when no file could be read as a picture or the engine cannot write
    /// PDFs from pictures. Unsubscribing stops reading and writing.
    /// </returns>
    public static IObservable<int> Convert(AppServices services, IReadOnlyList<string> paths, string destination)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        return services.Engine is not IPictureDocumentWriter writer
            ? Signal.Return(0)
            : Signal.Start(() => Read(services, paths), TaskPoolSequencer.Default)
                .SwitchMap(pictures => pictures.Length == 0 ? Signal.Return(0) : Write(writer, pictures, destination));
    }

    /// <summary>Writes the pictures beside the destination first, so a failure never leaves half a PDF where one was asked for.</summary>
    /// <param name="writer">The engine's picture document writer.</param>
    /// <param name="pictures">The pictures.</param>
    /// <param name="destination">The PDF file to write.</param>
    /// <returns>A cold signal giving the number of pages written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<int> Write(IPictureDocumentWriter writer, PageImageSource[] pictures, string destination) =>
        CopyFile.WriteSignal(destination, ".writing", stream => writer.WritePictures(pictures, stream).Select(_ => pictures.Length), static _ => true);

    /// <summary>Reads each picture in order, skipping files that are not pictures.</summary>
    /// <param name="services">The app services.</param>
    /// <param name="paths">The picture files.</param>
    /// <returns>The pictures that were read.</returns>
    private static PageImageSource[] Read(AppServices services, IReadOnlyList<string> paths)
    {
        var pictures = new List<PageImageSource>(paths.Count);
        foreach (var path in paths)
        {
            if (services.Head.LoadImage(path) is { } decoded)
            {
                pictures.Add(new(decoded.Pixels, decoded.Width, decoded.Height));
            }
        }

        return [.. pictures];
    }
}
