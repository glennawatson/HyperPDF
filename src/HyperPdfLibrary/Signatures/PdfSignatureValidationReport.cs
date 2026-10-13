// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>Everything known about one signature after checking it against the file.</summary>
/// <param name="Signature">The signature's field and modification-detection data.</param>
/// <param name="ByteRange">The byte range check, including the revision the signature closes.</param>
/// <param name="ModifiedAfterSigning">Whether anything but whitespace was appended after the signed bytes.</param>
/// <param name="Changes">The objects later revisions added, changed or removed, with whether each is permitted.</param>
/// <param name="Cms">The cryptographic check.</param>
[DebuggerDisplay("PdfSignatureValidationReport: {Signature.FieldName} signature {SignatureValid} modified {ModifiedAfterSigning}")]
public sealed record PdfSignatureValidationReport(
    PdfSignatureDetails Signature,
    PdfByteRangeCheck ByteRange,
    bool ModifiedAfterSigning,
    PdfObjectChange[] Changes,
    PdfCmsResult Cms)
{
    /// <summary>Gets a value indicating whether the signature covers the whole file.</summary>
    public bool CoversWholeDocument => ByteRange.CoversWholeDocument;

    /// <summary>Gets the kinds of change made after signing.</summary>
    public PdfModificationKinds ChangeKinds
    {
        get
        {
            var kinds = PdfModificationKinds.None;
            foreach (var change in Changes)
            {
                kinds |= change.Kind;
            }

            return kinds;
        }
    }

    /// <summary>Gets a value indicating whether DocMDP and field locks allow every change made after signing.</summary>
    public bool PermittedByMdp
    {
        get
        {
            foreach (var change in Changes)
            {
                if (!change.IsPermitted)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Gets a value indicating whether the hash of the signed bytes matches the signature's.</summary>
    public bool DigestValid => Cms.DigestValid;

    /// <summary>Gets a value indicating whether the signature over that hash is good.</summary>
    public bool SignatureValid => Cms.SignatureValid;

    /// <summary>
    /// Gets a value indicating whether the signature's digest is one the field's seed value /DigestMethod accepts; true
    /// when the field names no digests. Check <see cref="PdfSeedValueDigest.IsRequired"/> to tell a broken requirement
    /// from an ignored recommendation.
    /// </summary>
    public bool DigestMatchesSeedValue =>
        Signature.SeedDigest is not { } seed || (Cms.Digest != PdfDigestMethod.Unknown && Array.IndexOf(seed.Methods, Cms.Digest) >= 0);

    /// <summary>Gets the signer's certificate, or <see langword="null"/>.</summary>
    public X509Certificate2? Signer => Cms.Signer;

    /// <summary>Gets the signing time from the signed attributes, or <see langword="null"/>.</summary>
    public DateTimeOffset? SignedAttributeTime => Cms.SigningTime;

    /// <summary>Gets the signing time the signature dictionary records in /M, or <see langword="null"/>.</summary>
    public DateTimeOffset? RecordedTime => Signature.Field.SigningTime;

    /// <summary>Gets a value indicating whether the signature carries a timestamp, or is a document timestamp.</summary>
    public bool HasTimestamp => Cms.Timestamp is not null;

    /// <summary>Gets a value indicating whether the timestamp is valid.</summary>
    public bool TimestampValid => Cms.Timestamp?.IsValid == true;

    /// <summary>Gets the signer's chain, or <see langword="null"/>.</summary>
    public PdfChainResult? Chain => Cms.Chain;
}
