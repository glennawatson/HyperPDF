// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Attachments;

/// <summary>Reads the files embedded in a document. Safe to call from any thread.</summary>
public interface IAttachmentSource : IDocumentFeature
{
    /// <summary>Gets the embedded files, read once.</summary>
    /// <returns>The attachments.</returns>
    IReadOnlyList<DocumentAttachment> GetAttachments();

    /// <summary>Copies an embedded file's contents into a stream.</summary>
    /// <param name="index">The attachment index.</param>
    /// <param name="destination">The stream receiving the contents.</param>
    /// <returns><see langword="true"/> when the whole file was written.</returns>
    bool SaveAttachment(int index, Stream destination);
}
