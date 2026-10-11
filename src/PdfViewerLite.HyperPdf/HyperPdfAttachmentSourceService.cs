// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IAttachmentSource through the owning document.</summary>
internal sealed class HyperPdfAttachmentSourceService : IAttachmentSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAttachmentSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfAttachmentSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentAttachment> GetAttachments() => HyperPdfDocumentAttachments.GetAttachments(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SaveAttachment(int index, Stream destination) => HyperPdfDocumentAttachments.SaveAttachment(
            _owner,
            index,
            destination);
}
