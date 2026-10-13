// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Core.Creation;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Engine;

/// <summary>Maps the viewer's creation requests onto the library's creator.</summary>
internal static class DocumentCreationMapping
{
    /// <summary>Maps a request to the creator's options.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The options.</returns>
    internal static PdfDocumentCreatorOptions ToOptions(DocumentCreationRequest request) => new()
    {
        Title = request.Title,
        Author = request.Author,
        Subject = request.Subject,
        Keywords = request.Keywords,
        Language = request.Language,
        Tagged = request.Accessible,
        UaClaim = request.Accessible ? PdfUaConformance.Part2 : PdfUaConformance.None,
        CreatorTool = "HyperPDF",
    };

    /// <summary>Adds the font the text is drawn in.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="font">The font file to embed, or <see langword="null"/> for Helvetica.</param>
    /// <returns>The font to draw with.</returns>
    /// <exception cref="ArgumentException">The file is not a font that may be embedded.</exception>
    internal static PdfCanvasFont AddFont(PdfDocumentCreator creator, CreationFont? font)
    {
        if (font is null)
        {
            return creator.AddBuiltInFont(AppearanceFont.Helvetica);
        }

        var bytes = MemoryMarshal.TryGetArray(font.FileBytes, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : font.FileBytes.ToArray();
        var opened = PdfFontSource.Open(bytes, font.FaceIndex);
        return opened.Source is { } source
            ? creator.EmbedFont(source)
            : throw new ArgumentException(opened.Refusal ?? "The font file cannot be embedded.", nameof(font));
    }

    /// <summary>Adds an image to the document.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="image">The image.</param>
    /// <param name="cancellationToken">Cancels the encoding.</param>
    /// <returns>The image block to lay out.</returns>
    /// <exception cref="ArgumentException">The image cannot be stored.</exception>
    internal static PdfFlowImage AddImage(PdfDocumentCreator creator, CreationImage image, CancellationToken cancellationToken)
    {
        var created = image.Format switch
        {
            CreationImageFormat.Jpeg => creator.AddJpeg(image.Data.Span, PdfImageOptions.Default),
            CreationImageFormat.None => throw new ArgumentException("The image has no format.", nameof(image)),
            _ => creator.AddRaster(new(ToRaster(image.Format), image.Width, image.Height, image.Data), PdfImageOptions.Default, cancellationToken),
        };
        return new(created, image.AlternativeText, image.WidthPoints);
    }

    /// <summary>Maps a pixel format.</summary>
    /// <param name="format">The viewer's format.</param>
    /// <returns>The library's format.</returns>
    private static PdfRasterFormat ToRaster(CreationImageFormat format) => format switch
    {
        CreationImageFormat.Bgra32 => PdfRasterFormat.Bgra32,
        CreationImageFormat.Rgb24 => PdfRasterFormat.Rgb24,
        CreationImageFormat.Gray8 => PdfRasterFormat.Gray8,
        _ => PdfRasterFormat.Bilevel1,
    };
}
