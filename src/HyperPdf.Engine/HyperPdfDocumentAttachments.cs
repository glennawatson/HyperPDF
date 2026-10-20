// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Attachments;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentAttachments over the document's owned state.</summary>
internal static class HyperPdfDocumentAttachments
{
    /// <summary>Gets the embedded files, read once.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The attachments.</returns>
    internal static IReadOnlyList<DocumentAttachment> GetAttachments(HyperPdfDocument self)
    {
        // An edit drops this cache under the page write lock, so a list read here is never published after that drop.
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return [];
        }

        if (Volatile.Read(ref self.Attachments) is { } cached)
        {
            return cached;
        }

        var source = PdfDocumentAttachments.GetAttachments(self.Document);
        var onPages = AnnotationFiles(self);
        var attachments = new DocumentAttachment[source.Count + onPages.Count];
        for (var i = 0; i < source.Count; i++)
        {
            attachments[i] = new(source[i].Index, source[i].Name, HyperPdfAttachments.AttachmentSize(source[i].Data));
        }

        for (var i = 0; i < onPages.Count; i++)
        {
            var file = onPages[i];
            var index = source.Count + i;
            var name = string.IsNullOrEmpty(file.FileName) ? string.Create(CultureInfo.InvariantCulture, $"Attachment {index + 1}") : file.FileName;
            attachments[index] = new(index, name, HyperPdfAttachments.AttachmentSize(file.Data)) { PageIndex = file.PageIndex, AnnotationId = file.Id.Number };
        }

        if (ReadPortfolio(self) is { } portfolio)
        {
            HyperPdfPortfolioView.Apply(portfolio, attachments);
        }

        Volatile.Write(ref self.Attachments, attachments);
        return attachments;
    }

    /// <summary>Gets how the document's portfolio asks for its files to be shown.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The summary, or <see langword="null"/> when the document has no readable portfolio.</returns>
    internal static PortfolioSummary? GetPortfolioSummary(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || ReadPortfolio(self) is not { } portfolio ? null : HyperPdfPortfolioView.Summarise(portfolio);
    }

    /// <summary>Gets the file attachment annotations of the document that hold a file, in page order.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The annotations.</returns>
    internal static List<PdfFileAttachmentAnnotation> AnnotationFiles(HyperPdfDocument self)
    {
        var onPages = new List<PdfFileAttachmentAnnotation>();
        foreach (var annotation in PdfDocumentFileAttachmentAnnotations.GetFileAttachmentAnnotations(self.Document))
        {
            if (annotation.Data is not null)
            {
                onPages.Add(annotation);
            }
        }

        return onPages;
    }

    /// <summary>Copies an embedded file's contents into a stream.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="index">The attachment index.</param>
    /// <param name="destination">The stream receiving the contents.</param>
    /// <returns><see langword="true"/> when the whole file was written.</returns>
    internal static bool SaveAttachment(HyperPdfDocument self, int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var output = default(PooledBuffer);
        try
        {
            if (!Decode(self, index, ref output))
            {
                return false;
            }

            destination.Write(output.WrittenSpan);
            return true;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes an embedded file while no edit runs, so the index still names the file the caller listed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="index">The attachment index.</param>
    /// <param name="output">Receives the contents.</param>
    /// <returns><see langword="true"/> when the file exists and has contents.</returns>
    private static bool Decode(HyperPdfDocument self, int index, ref PooledBuffer output)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || index < 0)
        {
            return false;
        }

        var attachments = PdfDocumentAttachments.GetAttachments(self.Document);
        var data = index < attachments.Count ? attachments[index].Data : AnnotationFileData(self, index - attachments.Count);
        if (data is null)
        {
            return false;
        }

        _ = data.Decode(ref output);
        return true;
    }

    /// <summary>Reads the document's portfolio, treating damaged collection data as no portfolio.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The portfolio, or <see langword="null"/>.</returns>
    private static PdfPortfolio? ReadPortfolio(HyperPdfDocument self)
    {
        try
        {
            return PdfDocumentPortfolio.GetPortfolio(self.Document);
        }
        catch (PdfException)
        {
            return null;
        }
    }

    /// <summary>Gets the stream of the file a file attachment annotation holds.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="position">The annotation's position among the annotations that hold a file.</param>
    /// <returns>The stream, or <see langword="null"/> when there is no such annotation.</returns>
    private static PdfStream? AnnotationFileData(HyperPdfDocument self, int position)
    {
        var onPages = AnnotationFiles(self);
        return (uint)position < (uint)onPages.Count ? onPages[position].Data : null;
    }
}
