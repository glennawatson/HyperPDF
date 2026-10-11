// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>Opens PDFs encrypted for a recipient certificate.</summary>
public interface ICertificateDocumentEngine
{
    /// <summary>Opens a document using a PKCS #12 certificate file and its password.</summary>
    /// <param name="path">The document path.</param>
    /// <param name="certificatePath">The certificate file path.</param>
    /// <param name="certificatePassword">The certificate file password; it is not saved to preferences.</param>
    /// <returns>The opened document.</returns>
    IDocument OpenWithCertificate(string path, string certificatePath, string certificatePassword);

    /// <summary>Opens a document using a PKCS #12 certificate file with cancellable I/O.</summary>
    /// <param name="path">The document path.</param>
    /// <param name="certificatePath">The certificate file path.</param>
    /// <param name="certificatePassword">The certificate file password; it is not saved to preferences.</param>
    /// <param name="cancellationToken">Cancels certificate and document reads.</param>
    /// <returns>The opened document.</returns>
    ValueTask<IDocument> OpenWithCertificateAsync(string path, string certificatePath, string certificatePassword, CancellationToken cancellationToken);
}
