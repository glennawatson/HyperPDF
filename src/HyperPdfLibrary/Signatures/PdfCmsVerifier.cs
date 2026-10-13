// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Checks a signature's CMS container with <see cref="SignedCms"/>, or <see cref="CmsShakeSignatures"/> for SHAKE256: the
/// message digest over the byte ranges, the signer's signature, its signed signing time and timestamp, and the signer's
/// chain. Never throws on damaged input; a container that cannot be read gives a <see cref="PdfCmsStatus.Malformed"/>
/// result, and a digest this computer cannot compute gives <see cref="PdfCmsStatus.Unsupported"/>, never a valid result.
/// </summary>
internal static class PdfCmsVerifier
{
    /// <summary>The length of a SHA-1 hash.</summary>
    private const int Sha1Length = 20;

    /// <summary>The tag and length bytes of a short OCTET STRING.</summary>
    private const int OctetStringHeader = 2;

    /// <summary>The numbers per byte range: offset and length.</summary>
    private const int Pair = 2;

    /// <summary>The unsigned attribute holding a signature's RFC 3161 timestamp token.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>The SHA-1 OID, the digest of <c>adbe.pkcs7.sha1</c> content.</summary>
    private const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>Checks a signature.</summary>
    /// <param name="details">The signature.</param>
    /// <param name="range">The byte range check.</param>
    /// <param name="file">The file.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    internal static PdfCmsResult Verify(PdfSignatureDetails details, in PdfByteRangeCheck range, PdfByteSource file, CmsContext context)
    {
        var contents = details.Field.Contents;
        if (contents.Length == 0)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Unsigned, "The field is not signed.");
        }

        if (!range.IsValid)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.ByteRangeInvalid, $"The byte range is not valid: {range.Status}.");
        }

        if (!TryGetEncodedLength(contents, out var length))
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, "The signature is not a DER-encoded container.");
        }

        var signed = ReadSigned(file, details.Field.ByteRange, range.SignedLength);
        try
        {
            return details.Format switch
            {
                PdfSignatureFormat.Rfc3161 => VerifyDocumentTimestamp(signed, contents.AsMemory(0, length), context),
                PdfSignatureFormat.X509RsaSha1 => PdfCmsResult.Failed(PdfCmsStatus.Unsupported, "adbe.x509.rsa_sha1 signatures are not checked."),
                PdfSignatureFormat.Pkcs7Sha1 => VerifyContainer(signed, contents.AsSpan(0, length), false, context),
                _ => VerifyContainer(signed, contents.AsSpan(0, length), true, context),
            };
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException or InvalidOperationException)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, ex.Message);
        }
    }

    /// <summary>Gets the length of the DER value at the start of /Contents, so the zero padding after it is ignored.</summary>
    /// <param name="contents">The padded contents.</param>
    /// <param name="length">The encoded length.</param>
    /// <returns><see langword="true"/> when the contents start with a complete value.</returns>
    internal static bool TryGetEncodedLength(ReadOnlySpan<byte> contents, out int length)
    {
        try
        {
            _ = AsnDecoder.ReadEncodedValue(contents, AsnEncodingRules.BER, out _, out _, out length);
            return true;
        }
        catch (AsnContentException)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// Reads the signed parts of the file into one buffer, through the file's source. The buffer lives only for the check:
    /// <see cref="SignedCms"/> takes detached content as one array.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="range">The byte range, already checked.</param>
    /// <param name="length">The total signed length.</param>
    /// <returns>The signed bytes.</returns>
    private static byte[] ReadSigned(PdfByteSource file, long[] range, long length)
    {
        var signed = new byte[length];
        var written = 0;
        for (var i = 0; i < range.Length; i += Pair)
        {
            var part = signed.AsSpan(written, (int)range[i + 1]);
            written += file.Read(range[i], part);
        }

        return signed;
    }

    /// <summary>Checks a CMS signature: detached over the signed bytes, or enveloping their SHA-1 hash.</summary>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="der">The CMS container.</param>
    /// <param name="detached">Whether the content is the signed bytes rather than their SHA-1 hash.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    private static PdfCmsResult VerifyContainer(byte[] signed, ReadOnlySpan<byte> der, bool detached, CmsContext context)
    {
        var cms = detached ? new SignedCms(new ContentInfo(signed), true) : new SignedCms();
        cms.Decode(der);
        if (cms.SignerInfos.Count == 0)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.MissingSigner, "The signature has no signer.");
        }

        var signer = cms.SignerInfos[0];
        var certificate = signer.Certificate ?? CmsSignerChecks.Find(signer.SignerIdentifier, context.StoreCertificates);
        var check = CheckSigner(der, signer, signed, detached, certificate, context);
        var algorithm = signer.DigestAlgorithm.Value ?? string.Empty;
        if (Refused(check, certificate, signer) is { } refused)
        {
            return refused;
        }

        var signatureValid = check.Signature == CmsSignatureStatus.Valid;
        var digestValid = detached ? DigestMatches(signer, check.Digest, signed, signatureValid) : EnvelopedDigestMatches(cms, signed) && signatureValid;
        var timestamp = ReadTimestamp(signer, context);
        if (check.Signature == CmsSignatureStatus.Unsupported)
        {
            var detail = CmsCheckText.UnsupportedSignature(check.Digest);
            return new(PdfCmsStatus.Unsupported, digestValid, false, certificate, algorithm, SigningTime(signer), timestamp, null, detail);
        }

        if (certificate is null)
        {
            const string Missing = "The signer's certificate is not in the signature or the security store.";
            return new(PdfCmsStatus.MissingSigner, digestValid, false, null, algorithm, SigningTime(signer), timestamp, null, Missing);
        }

        var chain = PdfChainBuilder.Build(certificate, cms.Certificates, context, CheckTime(context, timestamp));
        return new(PdfCmsStatus.Checked, digestValid, signatureValid, certificate, algorithm, SigningTime(signer), timestamp, chain, string.Empty);
    }

    /// <summary>Checks the first signer's signature over detached or encapsulated content.</summary>
    /// <param name="der">The CMS container.</param>
    /// <param name="signer">The first signer.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="detached">Whether the content is detached.</param>
    /// <param name="certificate">The signer's certificate, or <see langword="null"/>.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The check.</returns>
    private static CmsSignerCheck CheckSigner(ReadOnlySpan<byte> der, SignerInfo signer, byte[] signed, bool detached, X509Certificate2? certificate, CmsContext context) =>
        detached
            ? CmsSignerChecks.CheckDetached(der, signer, signed, certificate, context.StoreCertificates)
            : CmsSignerChecks.CheckEnveloped(der, signer, certificate, context.StoreCertificates);

    /// <summary>Gets the time to check a signer's chain at: the option, a valid timestamp's time, or now.</summary>
    /// <param name="context">The store and options.</param>
    /// <param name="timestamp">The signature's timestamp, or <see langword="null"/>.</param>
    /// <returns>The time.</returns>
    private static DateTimeOffset CheckTime(CmsContext context, PdfTimestampResult? timestamp)
    {
        if (context.Options.VerificationTime is { } time)
        {
            return time;
        }

        return timestamp is { IsValid: true } ? timestamp.Time : context.Options.Clock.GetUtcNow();
    }

    /// <summary>Checks a document timestamp: a timestamp token whose imprint is the hash of the signed bytes.</summary>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="der">The token.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    private static PdfCmsResult VerifyDocumentTimestamp(byte[] signed, ReadOnlyMemory<byte> der, CmsContext context)
    {
        if (!Rfc3161TimestampToken.TryDecode(der, out var stamp, out _))
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, "The document timestamp could not be read.");
        }

        var info = stamp.TokenInfo;
        var oid = info.HashAlgorithmId.Value ?? string.Empty;
        var check = CmsTimestamps.Verify(stamp, signed, context.StoreCertificates, out var authority);
        var status = StatusOf(check.DigestStatus);
        if (status != PdfCmsStatus.Checked)
        {
            var notChecked = new PdfTimestampResult(info.Timestamp, false, authority, null) { Status = status };
            var detail = CmsCheckText.Describe(check.Digest, check.DigestStatus, oid);
            return new(status, false, false, authority, oid, info.Timestamp, notChecked, null, detail);
        }

        var at = context.Options.VerificationTime ?? info.Timestamp;
        var chain = authority is null ? null : PdfChainBuilder.Build(authority, stamp.AsSignedCms().Certificates, context, at);
        var result = new PdfTimestampResult(info.Timestamp, check.IsValid, authority, chain);
        return new(PdfCmsStatus.Checked, check.ImprintMatches, check.IsValid, authority, oid, info.Timestamp, result, chain, string.Empty);
    }

    /// <summary>Maps a digest status to the container status it gives.</summary>
    /// <param name="status">The digest status.</param>
    /// <returns><see cref="PdfCmsStatus.Checked"/> for an available digest.</returns>
    private static PdfCmsStatus StatusOf(CmsDigestStatus status) => status switch
    {
        CmsDigestStatus.Available => PdfCmsStatus.Checked,
        CmsDigestStatus.NotAllowed => PdfCmsStatus.DigestNotAllowed,
        _ => PdfCmsStatus.Unsupported,
    };

    /// <summary>
    /// Gives the result for a signer that is not checked because its digest is unavailable or not allowed, or that
    /// breaks an ISO/TS 32002 rule; neither is ever a valid signature.
    /// </summary>
    /// <param name="check">The signer check.</param>
    /// <param name="certificate">The signer's certificate, or <see langword="null"/>.</param>
    /// <param name="signer">The signer.</param>
    /// <returns>The result, or <see langword="null"/> when the signature was checked.</returns>
    private static PdfCmsResult? Refused(in CmsSignerCheck check, X509Certificate2? certificate, SignerInfo signer)
    {
        var algorithm = signer.DigestAlgorithm.Value ?? string.Empty;
        if (check.DigestStatus != CmsDigestStatus.Available)
        {
            var detail = CmsCheckText.Describe(check.Digest, check.DigestStatus, algorithm);
            return new(StatusOf(check.DigestStatus), false, false, certificate, algorithm, SigningTime(signer), null, null, detail);
        }

        return CmsCheckText.IsRuleViolation(check.Signature)
            ? new(RuleStatus(check.Signature), false, false, certificate, algorithm, SigningTime(signer), null, null, CmsCheckText.Rule(check.Signature, check.Digest))
            : null;
    }

    /// <summary>Maps a broken ISO/TS 32002 rule to the container status it gives.</summary>
    /// <param name="status">The signer check's signature status, one of the rule violations.</param>
    /// <returns>The container status.</returns>
    private static PdfCmsStatus RuleStatus(CmsSignatureStatus status) => status switch
    {
        CmsSignatureStatus.UnnamedCurve => PdfCmsStatus.UnnamedCurve,
        CmsSignatureStatus.DigestMismatch => PdfCmsStatus.DigestMismatch,
        _ => PdfCmsStatus.CurveDigestNotAllowed,
    };

    /// <summary>Compares the signed messageDigest attribute with the hash of the signed bytes.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="digest">The signer's digest algorithm, already checked as available.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="signatureValid">The signature result, used when there are no signed attributes and the signature covers the content directly.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool DigestMatches(SignerInfo signer, CmsDigestAlgorithm digest, byte[] signed, bool signatureValid)
    {
        if (FindMessageDigest(signer) is not { } expected)
        {
            return signer.SignedAttributes.Count == 0 && signatureValid;
        }

        Span<byte> actual = stackalloc byte[CmsDigests.MaxLength];
        var length = CmsDigests.Hash(digest, signed, actual);
        return length > 0 && CryptographicOperations.FixedTimeEquals(actual[..length], expected);
    }

    /// <summary>Finds the signed messageDigest attribute.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The digest, or <see langword="null"/>.</returns>
    private static byte[]? FindMessageDigest(SignerInfo signer)
    {
        foreach (var attribute in signer.SignedAttributes)
        {
            foreach (var value in attribute.Values)
            {
                if (value is Pkcs9MessageDigest digest)
                {
                    return digest.MessageDigest;
                }
            }
        }

        return null;
    }

    /// <summary>Compares an <c>adbe.pkcs7.sha1</c> container's content with the SHA-1 of the signed bytes.</summary>
    /// <param name="cms">The decoded container.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool EnvelopedDigestMatches(SignedCms cms, byte[] signed)
    {
        Span<byte> actual = stackalloc byte[Sha1Length];
        _ = PdfAlgorithms.Hash(Sha1Oid, signed, actual);
        var content = cms.ContentInfo.Content.AsSpan();

        // Writers store the hash either bare or wrapped in an OCTET STRING.
        return content.SequenceEqual(actual) || (content.Length == Sha1Length + OctetStringHeader && content[OctetStringHeader..].SequenceEqual(actual));
    }

    /// <summary>Reads and checks the timestamp token in a signer's unsigned attributes.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The timestamp, or <see langword="null"/> when there is none.</returns>
    private static PdfTimestampResult? ReadTimestamp(SignerInfo signer, CmsContext context)
    {
        foreach (var attribute in signer.UnsignedAttributes)
        {
            if (attribute.Oid.Value != TimestampTokenOid || attribute.Values.Count == 0)
            {
                continue;
            }

            if (!Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var stamp, out _))
            {
                return new(DateTimeOffset.MinValue, false, null, null);
            }

            // The token stamps the signer's signature value.
            var check = CmsTimestamps.Verify(stamp, signer.GetSignature(), context.StoreCertificates, out var authority);
            var time = stamp.TokenInfo.Timestamp;
            var status = StatusOf(check.DigestStatus);
            if (status != PdfCmsStatus.Checked)
            {
                return new(time, false, authority, null) { Status = status };
            }

            var chain = authority is null ? null : PdfChainBuilder.Build(authority, stamp.AsSignedCms().Certificates, context, context.Options.VerificationTime ?? time);
            return new(time, check.IsValid, authority, chain);
        }

        return null;
    }

    /// <summary>Reads the signed signing time attribute.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    private static DateTimeOffset? SigningTime(SignerInfo signer)
    {
        foreach (var attribute in signer.SignedAttributes)
        {
            foreach (var value in attribute.Values)
            {
                if (value is Pkcs9SigningTime time)
                {
                    return new DateTimeOffset(time.SigningTime.ToUniversalTime(), TimeSpan.Zero);
                }
            }
        }

        return null;
    }
}
