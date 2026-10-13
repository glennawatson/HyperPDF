// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Checks the PDF MAC of every revision of an encrypted document (ISO/TS 32004:2024). Each revision's trailer is read as
/// that revision wrote it, because a trailer read through the cross-reference chain would inherit an older revision's
/// <c>/AuthCode</c> and hide a revision that has none.
/// </summary>
internal static class PdfMacValidator
{
    /// <summary>The message for a revision whose structure cannot be read.</summary>
    private const string UnreadableRevision = "The revision's structure cannot be read, so its PDF MAC cannot be checked.";

    /// <summary>Gets the statuses from the one that says the document can be trusted least to the one that says it most.</summary>
    private static ReadOnlySpan<PdfMacStatus> WorstFirst => [PdfMacStatus.Invalid, PdfMacStatus.Missing, PdfMacStatus.Unsupported, PdfMacStatus.Valid];

    /// <summary>Checks the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The report; not applicable for a document that is not encrypted or that neither requires nor has a PDF MAC.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfMacReport Validate(PdfDocument document, CancellationToken cancellationToken)
    {
        var store = document.Objects;
        var names = PdfMacNames.From(store.Names);
        if (store.Security is not { } security || store.Trailer.GetDictionary(KnownName.Encrypt) is not { } current)
        {
            return PdfMacReport.NotApplicable;
        }

        var documentRequires = PdfMacRequirement.IsRequired(current, security);
        if (!documentRequires && !store.Trailer.ContainsKey(names.AuthCode))
        {
            return PdfMacReport.NotApplicable;
        }

        var revisions = PdfDocumentSignatureValidation.GetRevisionArray(document);
        var shared = new PdfMacShared(revisions, documentRequires);
        var results = new PdfMacRevisionResult[revisions.Length];
        for (var i = 0; i < results.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = CheckRevision(store, security, in shared, i, cancellationToken);
        }

        return new(Worst(results), results);
    }

    /// <summary>Gets the most serious status: invalid, missing, unsupported, valid, then not applicable.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The status.</returns>
    private static PdfMacStatus Worst(PdfMacRevisionResult[] results)
    {
        foreach (var status in WorstFirst)
        {
            foreach (var result in results)
            {
                if (result.Status == status)
                {
                    return status;
                }
            }
        }

        return PdfMacStatus.NotApplicable;
    }

    /// <summary>Checks one revision.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="security">The security handler.</param>
    /// <param name="shared">What every revision's check shares.</param>
    /// <param name="index">The revision.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult CheckRevision(PdfObjectStore store, PdfSecurityHandler security, in PdfMacShared shared, int index, CancellationToken cancellationToken)
    {
        var revision = shared.Revisions[index];
        PdfObjectStore raw;
        try
        {
            raw = StoreRevisions.OpenRawRevision(store, Math.Min(revision.EndOffset, store.Source.Length));
        }
        catch (PdfException)
        {
            return new(index, PdfMacStatus.Invalid, PdfMacLocation.None, UnreadableRevision);
        }

        // The revision shares the document's file and has no security handler, so disposing it only frees its own caches.
        using var owned = raw;
        PdfDictionary trailer;
        try
        {
            trailer = (revision.XrefOffset >= 0 ? StoreRevisions.ReadSectionTrailer(raw, revision.XrefOffset) : null) ?? raw.Trailer;
        }
        catch (PdfException)
        {
            return new(index, PdfMacStatus.Invalid, PdfMacLocation.None, UnreadableRevision);
        }

        // The revision's objects have their own name table, so the names are interned in it.
        var names = PdfMacNames.From(raw.Names);
        var encrypt = raw.Trailer.GetDictionary(KnownName.Encrypt);
        var required = shared.DocumentRequires || (encrypt is not null && PdfMacRequirement.IsRequired(encrypt, security));
        var value = trailer.GetRaw(names.AuthCode);
        if (value.IsNull)
        {
            return required
                ? new(index, PdfMacStatus.Missing, PdfMacLocation.None, PdfMacMessages.Required)
                : new(index, PdfMacStatus.NotApplicable, PdfMacLocation.None, PdfMacMessages.NotNeeded);
        }

        if (value.AsDictionary() is not { } authCode)
        {
            return new(index, PdfMacStatus.Invalid, PdfMacLocation.Unknown, PdfMacMessages.BadLayout);
        }

        return encrypt is not null && ReadSalt(encrypt, names) is { } salt
            ? Locate(new(store.Source, shared.Revisions, index, raw, authCode, security, salt, names), cancellationToken)
            : new(index, PdfMacStatus.Invalid, PdfMacLocation.Unknown, PdfMacMessages.BadEncrypt);
    }

    /// <summary>Sends the check to the standalone or the signature rules by the <c>/MACLocation</c>.</summary>
    /// <param name="context">The revision.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    private static PdfMacRevisionResult Locate(PdfMacCheckContext context, CancellationToken cancellationToken)
    {
        var location = context.AuthCode.GetName(context.Names.MacLocation);
        if (location == context.Names.Standalone)
        {
            return PdfMacStandalone.Check(context, cancellationToken);
        }

        if (location == context.Names.AttachedToSig)
        {
            return PdfMacAttached.Check(context, cancellationToken);
        }

        // Absence of /MACLocation is indicative of an invalid MAC; any other value is implementation-dependent (ISO/TS 32004 B.2.1).
        return location.IsNone
            ? new(context.Index, PdfMacStatus.Invalid, PdfMacLocation.Unknown, PdfMacMessages.BadLayout)
            : new(context.Index, PdfMacStatus.Unsupported, PdfMacLocation.Unknown, PdfMacMessages.UnknownLocation);
    }

    /// <summary>Reads <c>/KDFSalt</c>: a direct 32-byte string, in a handler of version 5 or later.</summary>
    /// <param name="encrypt">The Encrypt dictionary.</param>
    /// <param name="names">The names.</param>
    /// <returns>The salt, or <see langword="null"/> when it is missing or wrong.</returns>
    private static byte[]? ReadSalt(PdfDictionary encrypt, PdfMacNames names)
    {
        var value = encrypt.GetRaw(names.KdfSalt);
        return encrypt.GetInt32(KnownName.V) >= PdfMacKeys.MinimumVersion && value.Kind == PdfKind.String && value.AsStringBytes().Length == PdfMacKeys.SaltLength
            ? value.AsStringBytes().ToArray()
            : null;
    }

    /// <summary>What the checks of all revisions share.</summary>
    /// <param name="Revisions">The revisions.</param>
    /// <param name="DocumentRequires">Whether the current Encrypt dictionary requires a PDF MAC in every revision.</param>
    private readonly record struct PdfMacShared(PdfRevision[] Revisions, bool DocumentRequires);
}
