// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Takes the images a page draws out of a document as files. A JPEG or JPEG 2000 image is written as the document stores it,
/// so nothing is lost. A JBIG2 image is written as a JBIG2 file and a CCITT image as a TIFF, both with the coded data
/// untouched; when the CCITT coding has no TIFF form, or the image is inline, it is decoded like the rest. Any other image is
/// decoded and written as a PNG. An image drawn more than once is written once, and the images of forms the page draws are
/// included. Images the document cannot decode are left out.
/// </summary>
public static class PdfImageExtraction
{
    /// <summary>The deepest nesting of forms that is followed, so a form that draws itself cannot loop.</summary>
    private const int MaxFormDepth = 8;

    /// <summary>Gets the images a page draws.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The image files, in the order the page draws them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    public static PdfExtractedFile[] GetImages(PdfDocument document, int pageIndex)
    {
        var files = new List<PdfExtractedFile>();
        Collect(document, pageIndex, [], files);
        return [.. files];
    }

    /// <summary>Gets the images of every page in order, each image once even when several pages draw it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The image files, page by page.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async IAsyncEnumerable<PdfExtractedFile> GetImagesAsync(PdfDocument document, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HashSet<PdfObjectId> seen = [];
        var files = new List<PdfExtractedFile>();
        for (var page = 0; page < document.PageCount; page++)
        {
            await PdfDocumentPages.PrefetchPageAsync(document, page, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            files.Clear();
            Collect(document, page, seen, files);
            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    /// <summary>Collects the images of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="seen">The image streams already taken, which are not taken again.</param>
    /// <param name="files">Receives the files.</param>
    private static void Collect(PdfDocument document, int pageIndex, HashSet<PdfObjectId> seen, List<PdfExtractedFile> files)
    {
        var content = PdfDocumentPageContent.GetPageContent(document, pageIndex);
        var count = 0;
        CollectFrom(content, pageIndex, seen, files, ref count, 0);
    }

    /// <summary>Collects the images of one content, and of the forms it draws.</summary>
    /// <param name="content">The page or form content.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="seen">The image streams already taken.</param>
    /// <param name="files">Receives the files.</param>
    /// <param name="count">The images taken on this page so far, for naming.</param>
    /// <param name="depth">How many forms deep this content is.</param>
    private static void CollectFrom(PdfPageContent content, int pageIndex, HashSet<PdfObjectId> seen, List<PdfExtractedFile> files, ref int count, int depth)
    {
        foreach (var item in content.Objects)
        {
            if (item is PdfImageObject image && Encode(image, content.Resources) is { } file)
            {
                if (image.Stream is { Id.IsValid: true } stream && !seen.Add(stream.Id))
                {
                    continue;
                }

                count++;
                files.Add(new(pageIndex, Name(pageIndex, count, file.Extension), PdfExtractedFileKind.Image, file.Data));
            }
            else if (item is PdfFormObject form && depth < MaxFormDepth)
            {
                CollectFrom(form.GetContent(), pageIndex, seen, files, ref count, depth + 1);
            }
        }
    }

    /// <summary>Makes the file of an image.</summary>
    /// <param name="image">The image object.</param>
    /// <param name="resources">The resources the image's colour space may name.</param>
    /// <returns>The file's extension and bytes; <see langword="null"/> when the image cannot be read.</returns>
    private static ImageFile? Encode(PdfImageObject image, PdfDictionary? resources)
    {
        var last = image.Filters.Count > 0 ? image.Filters[^1].ToKnownName() : KnownName.None;
        var stored = last switch
        {
            KnownName.DCTDecode or KnownName.DCT => new ImageFile("jpg", image.GetDecodedData()),
            KnownName.JPXDecode => new("jp2", image.GetDecodedData()),
            KnownName.JBIG2Decode => Wrap("jb2", PdfStoredImageFiles.TryMakeJbig2(image)),
            KnownName.CCITTFaxDecode or KnownName.CCF => Wrap("tif", PdfStoredImageFiles.TryMakeCcittTiff(image)),
            _ => (ImageFile?)null,
        };

        if (stored is { } file)
        {
            return file;
        }

        var pixels = image.DecodePixels(resources);
        return pixels is null || pixels.Pixels.Length == 0 || pixels.UnsupportedCodec != Filters.PdfImageCodec.None ? null : new("png", PdfPngWriter.Encode(pixels));
    }

    /// <summary>Pairs wrapped data with its extension.</summary>
    /// <param name="extension">The extension without a dot.</param>
    /// <param name="data">The wrapped bytes, or <see langword="null"/> when the data could not be wrapped.</param>
    /// <returns>The file, or <see langword="null"/>.</returns>
    private static ImageFile? Wrap(string extension, byte[]? data) => data is null ? null : new ImageFile(extension, data);

    /// <summary>Names an image file.</summary>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="number">The image's number on the page, from 1.</param>
    /// <param name="extension">The extension without a dot.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Name(int pageIndex, int number, string extension) =>
        string.Create(CultureInfo.InvariantCulture, $"page-{pageIndex + 1}-image-{number}.{extension}");

    /// <summary>An image's extension and bytes.</summary>
    /// <param name="Extension">The extension without a dot.</param>
    /// <param name="Data">The bytes.</param>
    private readonly record struct ImageFile(string Extension, byte[] Data);
}
