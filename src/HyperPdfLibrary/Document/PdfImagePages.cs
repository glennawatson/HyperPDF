// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Makes pages from images: each image is stored once as an image XObject (ISO 32000-2 8.9) and drawn on a page of its own.</summary>
public static class PdfImagePages
{
    /// <summary>Gets the resource name of the image on its page.</summary>
    private static ReadOnlySpan<byte> ImageName => "Im0"u8;

    /// <summary>Makes a page from an image and puts it in a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the new page takes, from 0 to the page count.</param>
    /// <param name="image">The image.</param>
    /// <param name="options">The page size and placement.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index, the image size or an option is not usable.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InsertPage(PdfDocument document, int index, PdfPageImage image, PdfImagePageOptions options) =>
        InsertPage(document, index, image, options, CancellationToken.None);

    /// <summary>Makes a page from an image and puts it in a document, passing the token to the edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the new page takes, from 0 to the page count.</param>
    /// <param name="image">The image.</param>
    /// <param name="options">The page size and placement.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index, the image size or an option is not usable.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    public static void InsertPage(PdfDocument document, int index, PdfPageImage image, PdfImagePageOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, document.PageCount);
        var layout = PdfImageLayout.Create(image, options);
        PdfDocumentEditing.RunEdit(
            document,
            "Insert image page",
            PdfChangeKinds.PageChanges,
            new ImagePageEdit(document, index, image, layout),
            Apply);
    }

    /// <summary>Makes a page from an image and puts it in a document, after checking the token before the edit starts.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the new page takes, from 0 to the page count.</param>
    /// <param name="image">The image.</param>
    /// <param name="options">The page size and placement.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>A task completing after the edit.</returns>
    public static ValueTask InsertPageAsync(PdfDocument document, int index, PdfPageImage image, PdfImagePageOptions options, CancellationToken cancellationToken)
    {
        InsertPage(document, index, image, options, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <summary>Makes a new document with a page for each image.</summary>
    /// <param name="images">The images, one page each; at least one.</param>
    /// <param name="options">The page size and placement.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no images, or the pixels do not fill an image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An image size or an option is not usable.</exception>
    public static byte[] CreateDocument(ReadOnlySpan<PdfPageImage> images, PdfImagePageOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (images.IsEmpty)
        {
            throw new ArgumentException("A document needs at least one image.", nameof(images));
        }

        // A document needs a page to start from; the blank one is removed once the image pages are in.
        var builder = new PdfDocumentBuilder();
        _ = builder.AddSheet(1, 1, []);
        using var document = PdfDocumentReader.Open(builder.ToArray(), null);
        for (var i = 0; i < images.Length; i++)
        {
            InsertPage(document, document.PageCount, images[i], options, cancellationToken);
        }

        PdfDocumentPageOperations.DeletePages(document, [0]);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
    }

    /// <summary>Adds the image, its page and the page tree entry inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The new page.</param>
    private static void Apply(PdfEditTransaction transaction, ImagePageEdit edit)
    {
        var (document, index, image, layout) = edit;
        var store = document.Objects;
        var imageId = PdfImages.AddBgraImage(store, image.Pixels.Span, image.Width, image.Height);
        var content = default(PdfContentBuilder);
        try
        {
            content.SaveState();
            content.Transform(layout.ImageWidth, 0, 0, layout.ImageHeight, layout.X, layout.Y);
            content.DrawXObject(ImageName);
            content.RestoreState();
            var images = new PdfDictionary(store);
            images.Set(store.Names.Intern(ImageName), PdfValue.FromReference(imageId));
            var resources = new PdfDictionary(store);
            resources.Set(KnownName.XObject, PdfValue.FromDictionary(images));
            var page = new PdfDictionary(store);
            page.Set(KnownName.Type, PdfValue.FromName(KnownName.Page));
            page.Set(KnownName.MediaBox, PdfValue.FromArray(new PdfRectangle(0, 0, layout.PageWidth, layout.PageHeight).ToArray(store)));
            page.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
            var stream = new PdfStream(new(store), content.ToArray());
            page.Set(KnownName.Contents, PdfValue.FromReference(transaction.Add(PdfValue.FromStream(stream))));
            PdfDocumentPageOperations.InsertNewPage(document, transaction, index, page);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>An image page to add.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Index">The new page's index.</param>
    /// <param name="Image">The image.</param>
    /// <param name="Layout">The page and the image's place on it.</param>
    private sealed record ImagePageEdit(PdfDocument Document, int Index, PdfPageImage Image, PdfImageLayout Layout);
}
