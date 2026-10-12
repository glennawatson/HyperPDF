// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Saves a copy of a document as PDF/A. The conversion works on a private copy, so the document passed in never changes.
/// It writes the PDF/A identification and matching metadata, adds an output intent, embeds missing fonts from the
/// library's matching open fonts, and removes or fixes what the level forbids where that leaves the pages looking the
/// same. When something cannot be fixed, it stops and says why in plain words, and writes nothing. The written file is
/// checked again before it is copied to the destination.
/// </summary>
public static class PdfAConverter
{
    /// <summary>The Modify permission bit of an encrypted file (ISO 32000-2 Table 22, bit 4).</summary>
    private const int ModifyPermission = 8;

    /// <summary>Saves a copy of a document as PDF/A, using only font data already on this computer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level to meet.</param>
    /// <param name="destination">Receives the file when the conversion succeeds; nothing is written otherwise.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfAConversionResult SaveAsPdfA(PdfDocument document, PdfALevel level, Stream destination) =>
        SaveAsPdfA(document, level, destination, CancellationToken.None);

    /// <summary>Saves a copy of a document as PDF/A, using only font data already on this computer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level to meet.</param>
    /// <param name="destination">Receives the file when the conversion succeeds; nothing is written otherwise.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfAConversionResult SaveAsPdfA(PdfDocument document, PdfALevel level, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        var output = Convert(document, level, cancellationToken);
        if (output.File is { } file)
        {
            destination.Write(file);
        }

        return output.Result;
    }

    /// <summary>
    /// Saves a copy of a document as PDF/A. Font data the conversion needs is downloaded first when it is not on this
    /// computer yet.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level to meet.</param>
    /// <param name="destination">Receives the file when the conversion succeeds; nothing is written otherwise.</param>
    /// <param name="cancellationToken">Cancels the conversion and the downloads.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfAConversionResult> SaveAsPdfAAsync(PdfDocument document, PdfALevel level, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        await PdfAFontEmbedding.PrepareAsync(document, cancellationToken).ConfigureAwait(false);
        var output = Convert(document, level, cancellationToken);
        if (output.File is { } file)
        {
            await destination.WriteAsync(file, cancellationToken).ConfigureAwait(false);
        }

        return output.Result;
    }

    /// <summary>Converts a document, returning the file only when it passed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome and, when converted, the file.</returns>
    private static PdfAConversionOutput Convert(PdfDocument document, PdfALevel level, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        cancellationToken.ThrowIfCancellationRequested();
        if (CheckSource(document, level) is { } refused)
        {
            return refused;
        }

        // The conversion's own writes skip the PDF/A claim check: the copy must not change the caller's claim, and the
        // output is validated below, so checking it would only repeat the work, or repair a repair.
        byte[] copy;
        try
        {
            copy = PdfCompactWriter.SaveUnchecked(document.Objects, new(false, true));
        }
        catch (PdfException)
        {
            return Refused(level, "The document could not be read completely, so it cannot be converted.", PdfARule.CatalogMetadata);
        }

        using var working = PdfDocumentReader.Open(copy, null);
        var session = new PdfAConversionSession(working, level, cancellationToken);
        Fix(session);
        if (session.HasProblems)
        {
            return new(new(level, session.GetChanges(), session.GetProblems(), null), null);
        }

        var file = PdfCompactWriter.SaveUnchecked(working.Objects, new(session.Part != PdfAClaim.Part1, false));
        using var written = PdfDocumentReader.Open(file, null);
        var report = PdfAValidator.Validate(written, level, cancellationToken);
        foreach (var failed in report.Failed)
        {
            session.Refuse(failed);
        }

        return new(new(level, session.GetChanges(), session.GetProblems(), report), session.HasProblems ? null : file);
    }

    /// <summary>Refuses documents the conversion must not change: signed ones, and encrypted ones that forbid changes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <returns>The refusal, or <see langword="null"/> to go on.</returns>
    private static PdfAConversionOutput? CheckSource(PdfDocument document, PdfALevel level)
    {
        if (SignaturePresence.HasSignature(document.Objects))
        {
            return Refused(level, "The document is signed, and converting it would break the signatures. Remove the signatures first, then convert.", PdfARule.SignatureByteRange);
        }

        return document.Objects.Security is { } security && (security.Permissions & ModifyPermission) == 0
            ? Refused(level, "The document is protected against changes, and PDF/A does not allow encryption.", PdfARule.Encryption)
            : null;
    }

    /// <summary>Builds a refusal with one problem.</summary>
    /// <param name="level">The level.</param>
    /// <param name="description">The reason.</param>
    /// <param name="rule">The rule behind it.</param>
    /// <returns>The outcome.</returns>
    private static PdfAConversionOutput Refused(PdfALevel level, string description, PdfARule rule)
    {
        var note = new PdfAConversionNote(PdfALevels.Specification(level), Clause(level, rule), description);
        return new(new(level, [], [note], null), null);
    }

    /// <summary>Finds a rule's clause at a level.</summary>
    /// <param name="level">The level.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The clause, or empty when the level does not apply the rule.</returns>
    private static string Clause(PdfALevel level, PdfARule rule)
    {
        foreach (var reference in PdfARuleCatalog.For(level))
        {
            if (reference.Rule == rule)
            {
                return reference.Clause;
            }
        }

        return string.Empty;
    }

    /// <summary>Runs every fix on the working copy, in order.</summary>
    /// <param name="session">The conversion.</param>
    private static void Fix(PdfAConversionSession session)
    {
        var facts = PdfAConversionScan.Scan(session);
        PdfAConversionScanProblems.Report(session, facts);
        PdfAConversionForms.DrawAppearances(session);
        PdfAConversionVersion.Apply(session);
        PdfAConversionCatalog.Apply(session);
        PdfAConversionActions.Apply(session);
        PdfAConversionResources.Apply(session);
        PdfAConversionTransparency.Apply(session);
        PdfAConversionStreams.Apply(session);
        PdfAConversionAttachments.Apply(session);
        var space = PdfAConversionOutputIntent.Apply(session, facts);
        PdfAConversionAnnotations.Apply(session, space);
        PdfAFontEmbedding.Apply(session);
        PdfAConversionToUnicodeRepair.Apply(session);
        PdfAConversionCMaps.Check(session);
        PdfAConversionHalftones.Check(session);
        PdfAConversionMetadata.Write(session);
    }
}
