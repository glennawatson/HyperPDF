// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Takes hidden information out of a document.</summary>
internal sealed class HyperPdfDocumentSanitizerService : IDocumentSanitizer
{
    /// <summary>The document.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocumentSanitizerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfDocumentSanitizerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public SanitizeSummary Sanitize(SanitizeChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        lock (_owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
            using var access = HyperPdfNavigation.EnterPageWrite(_owner);
            var report = PdfDocumentSanitizer.Sanitize(_owner.Document, new(choices.Metadata, choices.Scripts, choices.Attachments, choices.Comments));
            HyperPdfNavigation.PagesEdited(_owner);
            return new(report.Scripts, report.Attachments, report.Comments, report.MetadataCleared);
        }
    }
}
