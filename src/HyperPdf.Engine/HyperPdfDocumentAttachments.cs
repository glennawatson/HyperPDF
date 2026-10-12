// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Attachments;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentAttachments over the document's owned state.</summary>
internal static class HyperPdfDocumentAttachments
{
    /// <summary>Gets the embedded files, read once.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The attachments.</returns>
    internal static IReadOnlyList<DocumentAttachment> GetAttachments(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return [];
        }

        if (Volatile.Read(ref self.Attachments) is { } cached)
        {
            return cached;
        }

        var source = PdfDocumentAttachments.GetAttachments(self.Document);
        var attachments = new DocumentAttachment[source.Count];
        for (var i = 0; i < attachments.Length; i++)
        {
            attachments[i] = new(source[i].Index, source[i].Name, HyperPdfAttachments.AttachmentSize(source[i].Data));
        }

        Volatile.Write(ref self.Attachments, attachments);
        return attachments;
    }

    /// <summary>Copies an embedded file's contents into a stream.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="index">The attachment index.</param>
    /// <param name="destination">The stream receiving the contents.</param>
    /// <returns><see langword="true"/> when the whole file was written.</returns>
    internal static bool SaveAttachment(HyperPdfDocument self, int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var attachments = self.IsDisposed ? [] : PdfDocumentAttachments.GetAttachments(self.Document);
        if ((uint)index >= (uint)attachments.Count || attachments[index].Data is not { } data)
        {
            return false;
        }

        var output = default(PooledBuffer);
        try
        {
            _ = data.Decode(ref output);
            destination.Write(output.WrittenSpan);
            return true;
        }
        finally
        {
            output.Dispose();
        }
    }
}
