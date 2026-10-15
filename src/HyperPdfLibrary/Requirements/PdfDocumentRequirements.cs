// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// Reads and checks the document requirements in the catalog's <c>/Requirements</c> array (ISO 32000-2 §12.11). Check
/// them before anything else runs: a document whose unmet penalties add up to 100 or more shall not be processed further
/// (§12.11.6), and a document without requirements costs nothing. The reader never runs a script, including the
/// alternative handlers a requirement names.
/// </summary>
public static class PdfDocumentRequirements
{
    /// <summary>Reads the requirements a document declares.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The requirements; <see cref="PdfRequirementList.Empty"/> when the catalog has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfRequirementList GetRequirements(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfReadSnapshots.Read(document, PdfReadKind.Requirements, static document => PdfRequirementReader.Read(document.Objects));
    }

    /// <summary>Reads the requirements a document declares.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The requirements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfRequirementList> GetRequirementsAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PdfDocumentRequirements.GetRequirements(document));
    }

    /// <summary>Checks a document's requirements against what HyperPDF provides.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A result for each requirement, the total penalty and what it allows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfRequirementAssessment Evaluate(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfReadSnapshots.Read(document, PdfReadKind.RequirementAssessment, static document => PdfDocumentRequirements.Evaluate(document, PdfRequirementCapabilities.HyperPdf));
    }

    /// <summary>Checks a document's requirements against a table of capabilities.</summary>
    /// <param name="document">The document.</param>
    /// <param name="capabilities">What the viewer provides.</param>
    /// <returns>A result for each requirement, the total penalty and what it allows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="capabilities"/> is <see langword="null"/>.</exception>
    public static PdfRequirementAssessment Evaluate(PdfDocument document, PdfRequirementCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return Evaluate(GetRequirements(document), capabilities);
    }

    /// <summary>Checks requirements already read against a table of capabilities.</summary>
    /// <param name="requirements">The requirements.</param>
    /// <param name="capabilities">What the viewer provides.</param>
    /// <returns>A result for each requirement, the total penalty and what it allows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requirements"/> or <paramref name="capabilities"/> is <see langword="null"/>.</exception>
    public static PdfRequirementAssessment Evaluate(PdfRequirementList requirements, PdfRequirementCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(capabilities);
        return PdfRequirementEvaluator.Evaluate(requirements, capabilities);
    }

    /// <summary>Checks a document's requirements against a table of capabilities.</summary>
    /// <param name="document">The document.</param>
    /// <param name="capabilities">What the viewer provides.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A result for each requirement, the total penalty and what it allows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="capabilities"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfRequirementAssessment> EvaluateAsync(PdfDocument document, PdfRequirementCapabilities capabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var list = await GetRequirementsAsync(document, cancellationToken).ConfigureAwait(false);
        return Evaluate(list, capabilities);
    }
}
