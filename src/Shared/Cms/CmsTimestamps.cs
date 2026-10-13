// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Checks RFC 3161 timestamp tokens whose message imprint or signer may use SHA-3 or SHAKE256, which
/// <see cref="Rfc3161TimestampToken"/> cannot hash itself: the data is hashed here and the token checked against the hash.
/// </summary>
internal static class CmsTimestamps
{
    /// <summary>Checks that a token stamps some data.</summary>
    /// <param name="token">The token.</param>
    /// <param name="data">The stamped data: the signed bytes of a document timestamp, or a signer's signature value.</param>
    /// <param name="extra">Extra certificates to find the authority's in.</param>
    /// <param name="authority">The authority's certificate, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    internal static CmsTimestampCheck Verify(Rfc3161TimestampToken token, ReadOnlySpan<byte> data, X509Certificate2Collection extra, out X509Certificate2? authority)
    {
        ArgumentNullException.ThrowIfNull(token);
        authority = null;
        var info = token.TokenInfo;
        var oid = info.HashAlgorithmId.Value;
        var algorithm = CmsDigests.FromOid(oid);
        var status = CmsDigests.IsShake(oid) ? ImprintStatus(info, out algorithm) : CmsDigests.Status(algorithm);
        if (status != CmsDigestStatus.Available)
        {
            return new(algorithm, status, false, false);
        }

        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        hash = hash[..CmsDigests.Hash(algorithm, data, hash)];
        var imprintMatches = CryptographicOperations.FixedTimeEquals(hash, info.GetMessageHash().Span);
        var cms = token.AsSignedCms();
        var signer = cms.SignerInfos[0];
        if (CmsDigests.IsShake(signer.DigestAlgorithm.Value))
        {
            var check = VerifyShakeSigned(cms, signer, extra, out authority);
            return check.DigestStatus == CmsDigestStatus.Available
                ? new(algorithm, status, imprintMatches, imprintMatches && check.Signature == CmsSignatureStatus.Valid)
                : new(check.Digest, check.DigestStatus, imprintMatches, false);
        }

        var signerAlgorithm = CmsDigests.FromOid(signer.DigestAlgorithm.Value);
        var signerStatus = CmsDigests.Status(signerAlgorithm);
        if (signerStatus != CmsDigestStatus.Available)
        {
            return new(signerAlgorithm, signerStatus, imprintMatches, false);
        }

        try
        {
            return new(algorithm, status, imprintMatches, token.VerifySignatureForHash(hash, info.HashAlgorithmId, out authority, extra));
        }
        catch (CryptographicException)
        {
            return new(algorithm, status, imprintMatches, false);
        }
    }

    /// <summary>
    /// Checks a token whose own signer uses SHAKE256, which <see cref="Rfc3161TimestampToken"/> cannot check. Unlike
    /// <see cref="Rfc3161TimestampToken.VerifySignatureForHash(ReadOnlySpan{byte}, Oid, out X509Certificate2?, X509Certificate2Collection?)"/>,
    /// this does not match the ESS signing-certificate attribute against the authority's certificate.
    /// </summary>
    /// <param name="cms">The token as a container.</param>
    /// <param name="signer">The token's signer.</param>
    /// <param name="extra">Extra certificates.</param>
    /// <param name="authority">The authority's certificate, or <see langword="null"/>.</param>
    /// <returns>The signer check.</returns>
    private static CmsSignerCheck VerifyShakeSigned(SignedCms cms, SignerInfo signer, X509Certificate2Collection extra, out X509Certificate2? authority)
    {
        authority = signer.Certificate ?? CmsSignerChecks.Find(signer.SignerIdentifier, extra);
        return CmsSignerChecks.CheckEnveloped(cms.Encode(), signer, authority, extra);
    }

    /// <summary>Reads the message imprint's AlgorithmIdentifier from the TSTInfo, so SHAKE parameters can be checked.</summary>
    /// <param name="info">The TSTInfo.</param>
    /// <param name="algorithm">The imprint's algorithm.</param>
    /// <returns>The status.</returns>
    private static CmsDigestStatus ImprintStatus(Rfc3161TimestampTokenInfo info, out CmsDigestAlgorithm algorithm)
    {
        algorithm = CmsDigestAlgorithm.Unknown;
        ReadOnlySpan<byte> encoded = info.Encode();
        var offset = 0;

        // TSTInfo: version, policy, then messageImprint, whose first value is the AlgorithmIdentifier (RFC 3161, 2.4.2).
        return CmsDer.TryReadChild(encoded, Asn1Tag.Sequence, out var tst)
            && CmsDer.TryNext(tst, ref offset, out _, out _, out _)
            && CmsDer.TryNext(tst, ref offset, out _, out _, out _)
            && CmsDer.TryNext(tst, ref offset, out _, out _, out var imprint)
            && CmsDer.TryRead(imprint, out _, out _, out var length)
            ? CmsDigests.ReadIdentifier(imprint[..length], out algorithm)
            : CmsDigestStatus.Unknown;
    }
}
