// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Keeps a document's PDF/A claim true when it is saved. The incremental, compact and linearized writers check the file
/// they are about to write against the level it claims. When it breaks a rule, they remove the claim from the document,
/// as an undoable edit, and write again, so no file is written with a false claim. The writers repair some defects as
/// they write, such as filtered metadata or a broken cross-reference table, so the written bytes are what is checked.
/// </summary>
internal static class PdfAClaimGuard
{
    /// <summary>The namespace of the PDF/A identification schema.</summary>
    private const string IdentificationNamespace = "http://www.aiim.org/pdfa/ns/id/";

    /// <summary>The prefix of the PDF/A identification schema.</summary>
    private const string IdentificationPrefix = "pdfaid";

    /// <summary>The undo label of the claim's removal.</summary>
    private const string RemoveLabel = "Remove PDF/A claim";

    /// <summary>Gets the identification properties that make up a claim: part, conformance, revision, amendment and corrigendum.</summary>
    private static XmpChange[] ClaimRemoval =>
    [
        new(new(IdentificationPrefix, IdentificationNamespace, "part", XmpShape.Simple), string.Empty),
        new(new(IdentificationPrefix, IdentificationNamespace, "conformance", XmpShape.Simple), string.Empty),
        new(new(IdentificationPrefix, IdentificationNamespace, "rev", XmpShape.Simple), string.Empty),
        new(new(IdentificationPrefix, IdentificationNamespace, "amd", XmpShape.Simple), string.Empty),
        new(new(IdentificationPrefix, IdentificationNamespace, "corr", XmpShape.Simple), string.Empty),
    ];

    /// <summary>Determines whether the document a writer is saving claims PDF/A, so its output must be checked.</summary>
    /// <param name="store">The objects being written.</param>
    /// <returns><see langword="true"/> when the catalog's XMP names a PDF/A part.</returns>
    internal static bool IsClaimed(PdfObjectStore store) => PdfAClaimReading.ReadPart(store) != 0;

    /// <summary>
    /// Checks a file a writer is about to write against the PDF/A level it claims. When it breaks a rule, the claim is
    /// removed from the document being written, and the writer must write again.
    /// </summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="output">The file the writer produced.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>What was found, or <see langword="null"/> when the file claims no level the standards define.</returns>
    /// <exception cref="PdfException">The claim must be removed, but the document's XMP packet cannot be edited.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfAClaimCheck? CheckOutput(PdfObjectStore store, byte[] output, CancellationToken cancellationToken)
    {
        var report = ValidateOutput(store, output, cancellationToken);
        if (report is null || report.Passed)
        {
            return report is null ? null : new(report.Level, false, []);
        }

        // A view over the writer's objects; it is not disposed, as disposing it would close them.
        PdfDocument view = new(store, null);
        PdfDocumentEditing.RunEdit(view, RemoveLabel, PdfChangeKinds.Metadata, view, static (transaction, state) => RemoveClaim(state, transaction));
        return new(report.Level, true, report.Failed);
    }

    /// <summary>Validates a written file against the level it claims.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="output">The file the writer produced.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The report, or <see langword="null"/> when the file claims no level the standards define.</returns>
    private static PdfAValidationReport? ValidateOutput(PdfObjectStore store, byte[] output, CancellationToken cancellationToken)
    {
        try
        {
            using var written = PdfDocumentReader.Open(output, null);
            return PdfAValidator.ClaimedLevel(written) is { } claimed ? PdfAValidator.Validate(written, claimed, cancellationToken) : null;
        }
        catch (PdfException exception) when (exception.Error == PdfError.Password)
        {
            // A file kept encrypted with an open password cannot be read back, and every PDF/A part forbids encryption,
            // so its claim is false (ISO 19005-1 to 4, §6.1.3).
            return PdfALevels.FromClaim(PdfAClaimReading.ReadClaim(store)) is { } level ? EncryptedReport(level) : null;
        }
    }

    /// <summary>Builds the report of an encrypted file: it breaks the rule against encryption.</summary>
    /// <param name="level">The level the file claims.</param>
    /// <returns>The report.</returns>
    private static PdfAValidationReport EncryptedReport(PdfALevel level)
    {
        var specification = PdfALevels.Specification(level);
        foreach (var (rule, clause, test) in PdfARuleCatalog.For(level))
        {
            if (rule != PdfARule.Encryption)
            {
                continue;
            }

            PdfARuleResult result = new(specification, clause, test, PdfARuleCatalog.Describe(rule), 1, PdfAContexts.Trailer);
            return new(level, [result], [result], []);
        }

        return new(level, [], [], []);
    }

    /// <summary>Removes the identification properties from the catalog's XMP packet.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The edit.</param>
    /// <exception cref="PdfException">The packet cannot be edited.</exception>
    private static void RemoveClaim(PdfDocument document, PdfEditTransaction transaction)
    {
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (!raw.IsReference || StoreReading.Resolve(document.Objects, raw).AsStream() is not { } stream
            || !XmpEditor.TryApply(stream.DecodeToArray(), ClaimRemoval, out var packet) || packet is null)
        {
            throw new PdfException(
                "The document claims PDF/A but no longer meets it, and its XMP metadata cannot be edited to remove the claim. "
                + "It was not saved, as the saved file would make a false PDF/A claim.");
        }

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
    }
}
