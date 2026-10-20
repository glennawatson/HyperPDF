// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Classifies each signature of a document against the PAdES baseline levels (ETSI EN 319 142-1) using only what is in the
/// file. Trust lists and online revocation checks are never made, and the report says so.
/// </summary>
public static class PdfPadesReader
{
    /// <summary>The ESS signing-certificate attribute, version 1 (RFC 2634).</summary>
    private const string SigningCertificateOid = "1.2.840.113549.1.9.16.2.12";

    /// <summary>The ESS signing-certificate attribute, version 2 (RFC 5035).</summary>
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";

    /// <summary>The content-type signed attribute.</summary>
    private const string ContentTypeOid = "1.2.840.113549.1.9.3";

    /// <summary>The message-digest signed attribute.</summary>
    private const string MessageDigestOid = "1.2.840.113549.1.9.4";

    /// <summary>The signing-time signed attribute.</summary>
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    /// <summary>The checks this reader never makes.</summary>
    private static readonly string[] NotPerformedChecks =
    [
        "Trust: whether the signer's certificate chains to a trusted root.",
        "Online revocation lookups.",
        "Whether the stored validation data covers every certificate in every chain.",
        "Whether the validation data was saved before the archive time stamp.",
    ];

    /// <summary>Classifies every signature in a document. Document time stamps are used as evidence and not listed themselves.</summary>
    /// <param name="document">The document.</param>
    /// <returns>One report per signature, in file order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfPadesReport[] Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(document);
        var result = new List<PdfPadesReport>(reports.Count);
        foreach (var report in reports)
        {
            if (report.Signature.IsSigned && !report.Signature.IsDocumentTimestamp)
            {
                result.Add(Classify(report, reports, store));
            }
        }

        return [.. result];
    }

    /// <summary>Classifies one signature.</summary>
    /// <param name="report">The signature's validation report.</param>
    /// <param name="all">Every signature's report, for later document time stamps.</param>
    /// <param name="store">The document security store.</param>
    /// <returns>The report.</returns>
    private static PdfPadesReport Classify(PdfSignatureValidationReport report, IReadOnlyList<PdfSignatureValidationReport> all, PdfSecurityStore store)
    {
        var findings = new List<PdfPadesFinding>();
        AddBaselineB(report, findings);
        AddBaselineT(report, all, findings);
        AddBaselineLt(store, findings);
        AddBaselineLta(report, all, findings);
        var level = PdfPadesLevel.None;
        for (var next = PdfPadesLevel.BaselineB; next <= PdfPadesLevel.BaselineLta && AllPassed(findings, next); next++)
        {
            level = next;
        }

        return new(report.Signature.FieldName, level, report.Cms is { Status: PdfCmsStatus.Checked, DigestValid: true, SignatureValid: true }, [.. findings], NotPerformedChecks);
    }

    /// <summary>Checks whether every finding at a level passed.</summary>
    /// <param name="findings">The findings.</param>
    /// <param name="level">The level.</param>
    /// <returns><see langword="true"/> when none at that level failed.</returns>
    private static bool AllPassed(List<PdfPadesFinding> findings, PdfPadesLevel level)
    {
        foreach (var finding in findings)
        {
            if (finding.Level == level && !finding.Passed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds the basic level checks: the CAdES sub-filter and the required signed attributes.</summary>
    /// <param name="report">The signature's report.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void AddBaselineB(PdfSignatureValidationReport report, List<PdfPadesFinding> findings)
    {
        const PdfPadesLevel Level = PdfPadesLevel.BaselineB;
        var cades = report.Signature.Format == PdfSignatureFormat.CadesDetached;
        var subFilterFailure = $"The sub-filter is {report.Signature.Format}; PAdES needs ETSI.CAdES.detached.";
        Add(findings, Level, "SubFilter", cades, "The sub-filter is ETSI.CAdES.detached.", subFilterFailure);
        var signed = ReadSignedAttributes(report.Signature.Field.Contents);
        var required = signed.Contains(ContentTypeOid) && signed.Contains(MessageDigestOid);
        Add(findings, Level, "ContentTypeAndDigest", required, "The content-type and message-digest attributes are present.", "Both the content-type and message-digest attributes are required.");
        var certificate = signed.Contains(SigningCertificateV2Oid) || signed.Contains(SigningCertificateOid);
        Add(findings, Level, "SigningCertificate", certificate, "The signing-certificate attribute is present.", "The signing-certificate attribute is missing.");
        var noTime = !signed.Contains(SigningTimeOid);
        Add(findings, Level, "SigningTimeAbsent", noTime, "No CMS signing time; the dictionary's /M is the claimed time.", "A CMS signing time is present; PAdES uses /M instead.");
    }

    /// <summary>Adds one finding, choosing its message by the outcome.</summary>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="level">The lowest level that needs the check.</param>
    /// <param name="rule">The rule name.</param>
    /// <param name="passed">Whether the check passed.</param>
    /// <param name="passMessage">The message when it passed.</param>
    /// <param name="failMessage">The message when it failed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(List<PdfPadesFinding> findings, PdfPadesLevel level, string rule, bool passed, string passMessage, string failMessage) =>
        findings.Add(new(level, rule, passed, passed ? passMessage : failMessage));

    /// <summary>Adds the time stamp level check: a valid signature time stamp, or a later valid document time stamp.</summary>
    /// <param name="report">The signature's report.</param>
    /// <param name="all">Every signature's report.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void AddBaselineT(PdfSignatureValidationReport report, IReadOnlyList<PdfSignatureValidationReport> all, List<PdfPadesFinding> findings)
    {
        var embedded = report.Cms.Timestamp is { IsValid: true };
        var documentStamp = FindLaterDocumentTimestamp(report, all) is not null;
        var passMessage = embedded ? "A valid signature time stamp is in the signature." : "A later valid document time stamp covers the signature.";
        Add(findings, PdfPadesLevel.BaselineT, "TimeStamp", embedded || documentStamp, passMessage, "There is no valid time stamp over the signature.");
    }

    /// <summary>Adds the long-term level checks: certificates and revocation data in the document security store.</summary>
    /// <param name="store">The document security store.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void AddBaselineLt(PdfSecurityStore store, List<PdfPadesFinding> findings)
    {
        const PdfPadesLevel Level = PdfPadesLevel.BaselineLt;
        var held = $"The document security store holds {store.Certificates.Length} certificates.";
        Add(findings, Level, "StoredCertificates", store.Certificates.Length > 0, held, "The document has no stored certificates.");
        Add(findings, Level, "StoredRevocationData", store.HasRevocationData, "The security store holds revocation data.", "The document has no stored OCSP responses or CRLs.");
    }

    /// <summary>Adds the archive level check: a later document time stamp that covers the whole file.</summary>
    /// <param name="report">The signature's report.</param>
    /// <param name="all">Every signature's report.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void AddBaselineLta(PdfSignatureValidationReport report, IReadOnlyList<PdfSignatureValidationReport> all, List<PdfPadesFinding> findings)
    {
        var stamp = FindLaterDocumentTimestamp(report, all);
        var passed = stamp is { CoversWholeDocument: true };
        Add(findings, PdfPadesLevel.BaselineLta, "ArchiveTimeStamp", passed, "A later document time stamp covers the whole file.", "No later valid document time stamp covers the whole file.");
    }

    /// <summary>Finds a valid document time stamp that closes a revision after a signature's.</summary>
    /// <param name="report">The signature's report.</param>
    /// <param name="all">Every signature's report.</param>
    /// <returns>The time stamp's report, or <see langword="null"/>.</returns>
    private static PdfSignatureValidationReport? FindLaterDocumentTimestamp(PdfSignatureValidationReport report, IReadOnlyList<PdfSignatureValidationReport> all)
    {
        foreach (var candidate in all)
        {
            var later = candidate.ByteRange.IsValid && report.ByteRange.IsValid && candidate.ByteRange.SignedEnd > report.ByteRange.SignedEnd;
            if (candidate.Signature.IsDocumentTimestamp && later && candidate.Cms is { Status: PdfCmsStatus.Checked, SignatureValid: true, DigestValid: true })
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Reads the OIDs of the first signer's signed attributes.</summary>
    /// <param name="contents">The padded /Contents bytes.</param>
    /// <returns>The attribute OIDs; empty when the container cannot be read.</returns>
    private static HashSet<string> ReadSignedAttributes(byte[] contents)
    {
        var oids = new HashSet<string>(StringComparer.Ordinal);
        if (!PdfCmsVerifier.TryGetEncodedLength(contents, out var length))
        {
            return oids;
        }

        try
        {
            var cms = new SignedCms();
            cms.Decode(contents.AsSpan(0, length));
            if (cms.SignerInfos.Count == 0)
            {
                return oids;
            }

            foreach (var attribute in cms.SignerInfos[0].SignedAttributes)
            {
                if (attribute.Oid.Value is { } oid)
                {
                    _ = oids.Add(oid);
                }
            }
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException)
        {
            return oids;
        }

        return oids;
    }
}
