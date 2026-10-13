// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Core.Signatures;

/// <summary>
/// Checks detached CMS (PKCS #7) PDF signatures: that the signed bytes match the signature, whether the signature covers
/// the whole file, and whether the signer's certificate chains to a certificate this computer trusts. The trust check
/// looks up revocation online with a short timeout, so the app only checks when the user asks to see the signatures.
/// </summary>
public static class SignatureVerifier
{
    /// <summary>The DER tag of a SEQUENCE, which a CMS blob starts with.</summary>
    private const byte SequenceTag = 0x30;

    /// <summary>The DER flag marking a long-form length.</summary>
    private const byte LongLengthFlag = 0x80;

    /// <summary>The most length bytes accepted.</summary>
    private const int MaxLengthBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A byte range holds offset and length pairs.</summary>
    private const int PairSize = 2;

    /// <summary>The unsigned attribute holding a signature's RFC 3161 timestamp token.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>The sub-filter of a document timestamp.</summary>
    private const string DocumentTimestampFilter = "ETSI.RFC3161";

    /// <summary>How long a revocation lookup may take.</summary>
    private static readonly TimeSpan RevocationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Checks a signature against the file it was read from.</summary>
    /// <param name="signature">The signature as stored.</param>
    /// <param name="filePath">The document's file.</param>
    /// <param name="extraTrust">Certificates to trust in addition to the system's, for example in tests.</param>
    /// <returns>The checked signature.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DocumentSignature Verify(RawSignature signature, string filePath, X509Certificate2Collection extraTrust) =>
        Verify(signature, filePath, extraTrust, DocumentSecurityStore.Read(File.ReadAllBytes(filePath)));

    /// <summary>
    /// Checks a signature against the file it was read from, using the file's document security store: its
    /// certificates help build the chain, and a valid trusted timestamp sets the time the certificate is checked at,
    /// so a signature made while the certificate was valid stays valid after it expires.
    /// </summary>
    /// <param name="signature">The signature as stored.</param>
    /// <param name="filePath">The document's file.</param>
    /// <param name="extraTrust">Certificates to trust in addition to the system's, for example in tests.</param>
    /// <param name="store">The file's document security store, read once for all its signatures.</param>
    /// <returns>The checked signature.</returns>
    public static DocumentSignature Verify(RawSignature signature, string filePath, X509Certificate2Collection extraTrust, DocumentSecurityStore store)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(extraTrust);
        ArgumentNullException.ThrowIfNull(store);
        using var file = File.OpenHandle(filePath);
        var length = RandomAccess.GetLength(file);
        if (!TryGetSignedLength(signature.ByteRange, length, out var signedLength) || !TryGetEncodedLength(signature.Contents, out var encodedLength))
        {
            return Unchecked(signature, "The signature's byte range or encoding is damaged.");
        }

        var signed = ReadSigned(file, signature.ByteRange, signedLength);
        var coversAll = signature.ByteRange[^PairSize] + signature.ByteRange[^1] == length;
        if (string.Equals(signature.SubFilter, DocumentTimestampFilter, StringComparison.Ordinal))
        {
            return VerifyDocumentTimestamp(signature, signed, signature.Contents.AsSpan(0, encodedLength), coversAll, new(extraTrust, store));
        }

        var cms = new SignedCms(new(signed), true);
        var der = signature.Contents.AsSpan(0, encodedLength);
        try
        {
            cms.Decode(der);
        }
        catch (CryptographicException ex)
        {
            return Unchecked(signature, ex.Message);
        }

        if (cms.SignerInfos.Count == 0 || cms.SignerInfos[0].Certificate is not { } certificate)
        {
            return Unchecked(signature, "The signature does not include the signer's certificate.");
        }

        var check = CmsSignerChecks.CheckDetached(der, cms.SignerInfos[0], signed, certificate, store.Certificates);
        if (NotChecked(signature, certificate, check, cms.SignerInfos[0].DigestAlgorithm.Value) is { } notChecked)
        {
            return notChecked;
        }

        var integrity = Integrity(check.Signature == CmsSignatureStatus.Valid, coversAll);
        var trust = new TrustSources(extraTrust, store);
        var timestamp = ReadSignatureTimestamp(cms.SignerInfos[0], trust);
        DateTimeOffset? checkedAt = timestamp is { IsValid: true } ? timestamp.Time : null;
        var trusted = IsTrusted(certificate, cms.Certificates, trust, checkedAt, out var detail);
        return new(
            signature.Index,
            certificate.GetNameInfo(X509NameType.SimpleName, false),
            certificate.GetNameInfo(X509NameType.SimpleName, true),
            GetSigningTime(cms.SignerInfos[0]) ?? signature.SigningTime,
            signature.Reason,
            integrity,
            trusted,
            detail) { Timestamp = timestamp, HasLongTermValidation = store.HasRevocationData, CheckedAt = checkedAt };
    }

    /// <summary>Gets the integrity from whether the signature matches and whether it covers the whole file.</summary>
    /// <param name="intact">Whether the signed bytes match.</param>
    /// <param name="coversAll">Whether the signature covers the whole file.</param>
    /// <returns>The integrity.</returns>
    private static SignatureIntegrity Integrity(bool intact, bool coversAll)
    {
        if (!intact)
        {
            return SignatureIntegrity.Invalid;
        }

        return coversAll ? SignatureIntegrity.Intact : SignatureIntegrity.ChangedAfterSigning;
    }

    /// <summary>Gets the integrity of a signature whose digest could not be used.</summary>
    /// <param name="status">The digest's status.</param>
    /// <returns>Not allowed, or unsupported.</returns>
    private static SignatureIntegrity UnsupportedIntegrity(CmsDigestStatus status) =>
        status == CmsDigestStatus.NotAllowed ? SignatureIntegrity.DigestNotAllowed : SignatureIntegrity.Unsupported;

    /// <summary>Checks a document timestamp: a timestamp token over the signed bytes instead of a person's signature.</summary>
    /// <param name="signature">The signature as stored.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="token">The timestamp token.</param>
    /// <param name="coversAll">Whether it covers the whole file.</param>
    /// <param name="trust">The extra trust and the document security store.</param>
    /// <returns>The checked document timestamp.</returns>
    private static DocumentSignature VerifyDocumentTimestamp(RawSignature signature, byte[] signed, ReadOnlySpan<byte> token, bool coversAll, in TrustSources trust)
    {
        if (!Rfc3161TimestampToken.TryDecode(token.ToArray(), out var decoded, out _))
        {
            return Unchecked(signature, "The document timestamp could not be read.");
        }

        var check = CmsTimestamps.Verify(decoded, signed, trust.Store.Certificates, out var authority);
        var supported = check.DigestStatus == CmsDigestStatus.Available;
        var valid = supported && check.IsValid;
        var time = decoded.TokenInfo.Timestamp;
        var trusted = authority is not null && IsTrusted(authority, decoded.AsSignedCms().Certificates, trust, time, out _);
        var name = authority?.GetNameInfo(X509NameType.SimpleName, false) ?? string.Empty;
        return new(
            signature.Index,
            name,
            authority?.GetNameInfo(X509NameType.SimpleName, true) ?? string.Empty,
            time,
            signature.Reason,
            supported ? Integrity(valid, coversAll) : UnsupportedIntegrity(check.DigestStatus),
            trusted,
            CmsCheckText.Describe(check.Digest, check.DigestStatus, decoded.TokenInfo.HashAlgorithmId.Value))
        {
            Timestamp = new(time, name, valid, trusted) { IsSupported = supported },
            IsDocumentTimestamp = true,
            HasLongTermValidation = trust.Store.HasRevocationData,
            CheckedAt = time,
        };
    }

    /// <summary>Reads and checks the timestamp on a signature: that it stamps this signature and that its authority is trusted.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="trust">The extra trust and the document security store.</param>
    /// <returns>The timestamp, or <see langword="null"/> when the signature has none.</returns>
    private static SignatureTimestamp? ReadSignatureTimestamp(SignerInfo signer, in TrustSources trust)
    {
        foreach (var attribute in signer.UnsignedAttributes)
        {
            if (attribute.Oid.Value != TimestampTokenOid || attribute.Values.Count == 0)
            {
                continue;
            }

            if (!Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var token, out _))
            {
                return new(DateTimeOffset.MinValue, string.Empty, false, false);
            }

            // The token stamps the signer's signature value.
            var check = CmsTimestamps.Verify(token, signer.GetSignature(), trust.Store.Certificates, out var authority);
            var supported = check.DigestStatus == CmsDigestStatus.Available;
            var valid = supported && check.IsValid;
            var time = token.TokenInfo.Timestamp;
            var trusted = authority is not null && IsTrusted(authority, token.AsSignedCms().Certificates, trust, time, out _);
            return new(time, authority?.GetNameInfo(X509NameType.SimpleName, false) ?? string.Empty, valid, trusted) { IsSupported = supported };
        }

        return null;
    }

    /// <summary>
    /// Gives the result for a signature whose digest or signature algorithm could not be checked, or whose digest PDF does
    /// not allow (ISO/TS 32001:2022, 5.1.4), so it is never reported as valid.
    /// </summary>
    /// <param name="signature">The signature as stored.</param>
    /// <param name="certificate">The signer's certificate.</param>
    /// <param name="check">The signer check.</param>
    /// <param name="oid">The signer's digest OID, named when the digest is unknown.</param>
    /// <returns>The result, or <see langword="null"/> when the signature was checked.</returns>
    private static DocumentSignature? NotChecked(RawSignature signature, X509Certificate2 certificate, in CmsSignerCheck check, string? oid)
    {
        var integrity = check switch
        {
            { DigestStatus: CmsDigestStatus.NotAllowed } => SignatureIntegrity.DigestNotAllowed,
            { DigestStatus: not CmsDigestStatus.Available } or { Signature: CmsSignatureStatus.Unsupported } => SignatureIntegrity.Unsupported,
            { Signature: CmsSignatureStatus.CurveDigestNotAllowed } => SignatureIntegrity.CurveDigestNotAllowed,
            { Signature: CmsSignatureStatus.UnnamedCurve } => SignatureIntegrity.UnnamedCurve,
            { Signature: CmsSignatureStatus.DigestMismatch } => SignatureIntegrity.DigestMismatch,
            _ => SignatureIntegrity.Unknown,
        };
        if (integrity == SignatureIntegrity.Unknown)
        {
            return null;
        }

        var detail = NotCheckedDetail(check, oid);
        return new(
            signature.Index,
            certificate.GetNameInfo(X509NameType.SimpleName, false),
            certificate.GetNameInfo(X509NameType.SimpleName, true),
            signature.SigningTime,
            signature.Reason,
            integrity,
            false,
            detail);
    }

    /// <summary>Explains in plain words why a signature was not checked or is not valid.</summary>
    /// <param name="check">The signer check.</param>
    /// <param name="oid">The signer's digest OID, named when the digest is unknown.</param>
    /// <returns>The explanation.</returns>
    private static string NotCheckedDetail(in CmsSignerCheck check, string? oid)
    {
        if (check.DigestStatus != CmsDigestStatus.Available)
        {
            return CmsCheckText.Describe(check.Digest, check.DigestStatus, oid);
        }

        return CmsCheckText.IsRuleViolation(check.Signature) ? CmsCheckText.Rule(check.Signature, check.Digest) : CmsCheckText.UnsupportedSignature(check.Digest);
    }

    /// <summary>Determines whether a certificate chains to a trusted root, as of a time.</summary>
    /// <param name="certificate">The signer's certificate.</param>
    /// <param name="included">Certificates included in the signature.</param>
    /// <param name="trust">Extra trusted roots and the document security store's certificates.</param>
    /// <param name="at">The time to check at, a trusted timestamp's; now when <see langword="null"/>.</param>
    /// <param name="detail">Why the chain is not trusted, or an empty string.</param>
    /// <returns><see langword="true"/> when trusted.</returns>
    private static bool IsTrusted(X509Certificate2 certificate, X509Certificate2Collection included, in TrustSources trust, DateTimeOffset? at, out string detail)
    {
        if (!CmsChainSupport.CanBuild(certificate))
        {
            detail = "This computer's certificate checker does not support the type of key in this certificate.";
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.UrlRetrievalTimeout = RevocationTimeout;
        chain.ChainPolicy.ExtraStore.AddRange(included);
        chain.ChainPolicy.ExtraStore.AddRange(trust.Store.Certificates);
        if (at is { } time)
        {
            chain.ChainPolicy.VerificationTime = time.UtcDateTime;
            chain.ChainPolicy.VerificationTimeIgnored = false;
        }

        if (trust.Extra.Count > 0)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(trust.Extra);
        }

        var trusted = chain.Build(certificate);
        detail = trusted || chain.ChainStatus.Length == 0 ? string.Empty : chain.ChainStatus[0].StatusInformation.Trim();
        return trusted;
    }

    /// <summary>Gets the signing time from the signed attributes.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    private static DateTimeOffset? GetSigningTime(SignerInfo signer)
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

    /// <summary>Validates a byte range against the file and totals its length.</summary>
    /// <param name="range">Offset and length pairs.</param>
    /// <param name="fileLength">The file length.</param>
    /// <param name="signedLength">The total signed length.</param>
    /// <returns><see langword="true"/> when the range lies within the file.</returns>
    private static bool TryGetSignedLength(long[] range, long fileLength, out int signedLength)
    {
        signedLength = 0;
        if (range.Length == 0 || range.Length % PairSize != 0)
        {
            return false;
        }

        long total = 0;
        for (var i = 0; i < range.Length; i += PairSize)
        {
            if (range[i] < 0 || range[i + 1] < 0 || range[i] + range[i + 1] > fileLength)
            {
                return false;
            }

            total += range[i + 1];
        }

        if (total > int.MaxValue)
        {
            return false;
        }

        signedLength = (int)total;
        return true;
    }

    /// <summary>Reads the DER length of a CMS blob, so the zero padding after it is ignored.</summary>
    /// <param name="contents">The padded blob.</param>
    /// <param name="length">The encoded length including tag and length bytes.</param>
    /// <returns><see langword="true"/> when the blob starts with a valid SEQUENCE.</returns>
    private static bool TryGetEncodedLength(byte[] contents, out int length)
    {
        length = 0;
        if (contents.Length < PairSize || contents[0] != SequenceTag)
        {
            return false;
        }

        int first = contents[1];
        if ((first & LongLengthFlag) == 0)
        {
            length = first + PairSize;
            return length <= contents.Length;
        }

        var count = first & ~LongLengthFlag;
        if (count is 0 or > MaxLengthBytes || contents.Length < PairSize + count)
        {
            return false;
        }

        long value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << ByteBits) | contents[PairSize + i];
        }

        value += PairSize + count;
        if (value > contents.Length)
        {
            return false;
        }

        length = (int)value;
        return true;
    }

    /// <summary>Reads the signed parts of the file into one buffer.</summary>
    /// <param name="file">The file.</param>
    /// <param name="range">Offset and length pairs.</param>
    /// <param name="signedLength">The total length.</param>
    /// <returns>The signed bytes.</returns>
    private static byte[] ReadSigned(SafeFileHandle file, long[] range, int signedLength)
    {
        var signed = new byte[signedLength];
        var written = 0;
        for (var i = 0; i < range.Length; i += PairSize)
        {
            var part = signed.AsSpan(written, (int)range[i + 1]);
            var offset = range[i];
            while (!part.IsEmpty)
            {
                var read = RandomAccess.Read(file, part, offset);
                if (read == 0)
                {
                    break;
                }

                part = part[read..];
                offset += read;
            }

            written += (int)range[i + 1];
        }

        return signed;
    }

    /// <summary>Creates the result for a signature that could not be checked.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    private static DocumentSignature Unchecked(RawSignature signature, string detail) =>
        new(signature.Index, string.Empty, string.Empty, signature.SigningTime, signature.Reason, SignatureIntegrity.Unknown, false, detail);

    /// <summary>Where trust comes from besides the system: extra roots, and the document's own security store.</summary>
    /// <param name="Extra">Extra trusted roots.</param>
    /// <param name="Store">The document security store.</param>
    private readonly record struct TrustSources(X509Certificate2Collection Extra, DocumentSecurityStore Store);
}
