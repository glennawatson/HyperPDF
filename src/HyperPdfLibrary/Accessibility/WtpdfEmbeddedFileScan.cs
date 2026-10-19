// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks the embedded files of the accessibility level (WTPDF 1.0, clause 7.2.2): an embedded file that is not a PDF and is
/// needed to understand the document carries a PDF Declaration of its own, in the <c>Metadata</c> of its embedded file stream.
/// Whether a file is needed is the author's judgement, so every non-PDF attachment without a declaration is reported.
/// </summary>
internal static class WtpdfEmbeddedFileScan
{
    /// <summary>Reports each non-PDF attachment whose stream has no declaration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    internal static void Run(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        foreach (var attachment in PdfDocumentAttachments.GetAttachments(document))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attachment.Description is null)
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.14.1", $"The embedded file '{attachment.Name}' needs a Desc entry in its file specification that says what it is.", 0));
            }

            CheckDeclaration(document, attachment, findings);
        }
    }

    /// <summary>Reports a non-PDF attachment whose stream has no declaration.</summary>
    /// <param name="document">The document.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckDeclaration(PdfDocument document, PdfAttachment attachment, List<PdfWtpdfFinding> findings)
    {
        if (attachment.Data is not { } stream || attachment.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if ((PdfDocumentMetadata.ReadXmp(document, stream.Dictionary)?.Declarations ?? []).Length != 0)
        {
            return;
        }

        findings.Add(new(
            PdfWtpdfProfile.Accessibility,
            "7.2.2",
            $"The embedded file '{attachment.Name}' has no PDF Declaration in its metadata. If it is needed to understand the document, it needs one that names an accessibility standard.",
            0));
    }
}
