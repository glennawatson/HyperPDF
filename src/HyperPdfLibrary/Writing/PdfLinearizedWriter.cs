// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Rewrites a document with first-page and hint sections for linearized PDF readers. A signed document is refused, as no
/// signature covers the rewritten bytes. Before writing, every trap network that no longer matches its page is removed
/// from the document, and one with the wrong flags or position is fixed (see <see cref="PdfSaveReport.TrapNetworks"/>).
/// </summary>
public static class PdfLinearizedWriter
{
    /// <summary>The maximum layout passes before refusing an unstable file.</summary>
    private const int MaxPasses = 8;

    /// <summary>Writes the document into a new linearized file array.</summary>
    /// <param name="document">The document to save.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">The document is signed, or cannot be written as a conforming linearized file.</exception>
    public static byte[] Save(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SignaturePresence.ThrowIfSigned(document.Objects);
        PdfMacSeal.ThrowIfNeeded(document.Objects, "Linearizing");
        PdfTrapNetworkGuard.Apply(document.Objects, true, null, CancellationToken.None);
        PdfRequirementsGuard.Apply(document.Objects, null, CancellationToken.None);
        return BuildChecked(document, null, CancellationToken.None);
    }

    /// <summary>Writes a linearized document to a stream asynchronously.</summary>
    /// <param name="document">The document to save.</param>
    /// <param name="destination">The writable stream.</param>
    /// <param name="cancellationToken">Cancels the save and stream write.</param>
    /// <returns>A task that completes after the output is written.</returns>
    /// <exception cref="PdfException">The document is signed, or cannot be written as a conforming linearized file.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task SaveAsync(PdfDocument document, Stream destination, CancellationToken cancellationToken) =>
        SaveAsync(document, destination, null, cancellationToken);

    /// <summary>
    /// Writes a linearized document to a stream asynchronously and reports what the user should know. A file that breaks
    /// the PDF/A level it claims is repaired, downloading font data the repair needs, and the repaired file is linearized
    /// and written instead.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="destination">The writable stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the save and stream write.</param>
    /// <returns>A task that completes after the output is written.</returns>
    /// <exception cref="PdfException">The document is signed, or cannot be written as a conforming linearized file.</exception>
    public static async Task SaveAsync(PdfDocument document, Stream destination, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        SignaturePresence.ThrowIfSigned(document.Objects);
        PdfMacSeal.ThrowIfNeeded(document.Objects, "Linearizing");
        PdfTrapNetworkGuard.Apply(document.Objects, true, report, cancellationToken);
        PdfRequirementsGuard.Apply(document.Objects, report, cancellationToken);
        var claimed = PdfAClaimGuard.IsClaimed(document.Objects);
        var bytes = Build(document.Objects, PdfLinearizationPlan.Create(document, cancellationToken), cancellationToken);
        if (claimed)
        {
            var outcome = await PdfAClaimGuard.CheckOutputAsync(document.Objects, bytes, cancellationToken).ConfigureAwait(false);
            bytes = Settle(document, bytes, report, outcome, cancellationToken);
        }

        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lays out and writes the file. A file that claims PDF/A is checked as written; when it breaks a rule, the repaired
    /// file is linearized instead, or, when it cannot be repaired, the claim is removed and the file is written again.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A complete linearized file.</returns>
    private static byte[] BuildChecked(PdfDocument document, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        var claimed = PdfAClaimGuard.IsClaimed(document.Objects);
        var bytes = Build(document.Objects, PdfLinearizationPlan.Create(document, cancellationToken), cancellationToken);
        return claimed ? Settle(document, bytes, report, PdfAClaimGuard.CheckOutput(document.Objects, bytes, cancellationToken), cancellationToken) : bytes;
    }

    /// <summary>Acts on the claim check of a written file.</summary>
    /// <param name="document">The document being saved.</param>
    /// <param name="bytes">The written file.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="outcome">The check, or <see langword="null"/> when the file claims no level.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The file to write.</returns>
    private static byte[] Settle(PdfDocument document, byte[] bytes, PdfSaveReport? report, PdfAClaimOutcome? outcome, CancellationToken cancellationToken)
    {
        if (outcome is not { } found)
        {
            return bytes;
        }

        report?.Claim = found.Check;
        if (found.Repaired is { } repaired)
        {
            using var copy = PdfDocumentReader.Open(repaired, null);
            return Build(copy.Objects, PdfLinearizationPlan.Create(copy, cancellationToken), cancellationToken);
        }

        return found.Check.Removed ? Build(document.Objects, PdfLinearizationPlan.Create(document, cancellationToken), cancellationToken) : bytes;
    }

    /// <summary>Repeats the write until all cross-reference and hint offsets are stable.</summary>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object ordering.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A complete linearized file.</returns>
    /// <exception cref="PdfException">The file layout does not stabilize.</exception>
    private static byte[] Build(PdfObjectStore store, PdfLinearizationPlan plan, CancellationToken cancellationToken)
    {
        var previous = new PdfLinearizationLayout(plan.HintNumber, plan.PageGroups.Length);
        var fileId = default(PdfValue);
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var writer = new PdfObjectWriter(store.Names, store.Security);
            try
            {
                var current = PdfLinearizationOutput.Write(ref writer, store, plan, previous, fileId, cancellationToken);
                if (current.Matches(previous))
                {
                    return writer.ToArray();
                }

                // The id is a hash written as a literal or hex string whose length follows its bytes, so a fresh id each
                // pass would move every later offset; the first pass's id is kept for the rest.
                if (pass == 0)
                {
                    fileId = current.FileId;
                }

                previous = current;
            }
            finally
            {
                writer.Dispose();
            }
        }

        throw new PdfException(PdfError.Format, "The linearized file positions did not settle before saving.");
    }
}
