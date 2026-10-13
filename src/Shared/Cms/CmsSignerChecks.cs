// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Checks a CMS signer's signature: with <see cref="SignedCms"/> for SHA-1, SHA-2 and SHA-3, and with
/// <see cref="CmsShakeSignatures"/> for SHAKE256. A digest this computer cannot compute, or one PDF does not allow,
/// gives an unsupported result instead of a failed signature.
/// </summary>
internal static class CmsSignerChecks
{
    /// <summary>Checks the first signer of a container whose content is detached.</summary>
    /// <param name="der">The container.</param>
    /// <param name="signer">The first signer, from the decoded container.</param>
    /// <param name="content">The detached content.</param>
    /// <param name="certificate">The signer's certificate, or <see langword="null"/> when it cannot be found.</param>
    /// <param name="extra">Extra certificates to find the signer's in.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CmsSignerCheck CheckDetached(ReadOnlySpan<byte> der, SignerInfo signer, ReadOnlySpan<byte> content, X509Certificate2? certificate, X509Certificate2Collection extra) =>
        Check(der, signer, content, true, certificate, extra);

    /// <summary>Checks the first signer of a container that encapsulates its content, such as a timestamp token.</summary>
    /// <param name="der">The container.</param>
    /// <param name="signer">The first signer, from the decoded container.</param>
    /// <param name="certificate">The signer's certificate, or <see langword="null"/> when it cannot be found.</param>
    /// <param name="extra">Extra certificates to find the signer's in.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CmsSignerCheck CheckEnveloped(ReadOnlySpan<byte> der, SignerInfo signer, X509Certificate2? certificate, X509Certificate2Collection extra) =>
        Check(der, signer, default, false, certificate, extra);

    /// <summary>Finds a signer's certificate among candidates.</summary>
    /// <param name="identifier">The signer's identifier.</param>
    /// <param name="candidates">The candidates.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    internal static X509Certificate2? Find(SubjectIdentifier identifier, X509Certificate2Collection candidates)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(candidates);
        foreach (var candidate in candidates)
        {
            if (identifier.MatchesCertificate(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Checks the first signer.</summary>
    /// <param name="der">The container.</param>
    /// <param name="signer">The first signer.</param>
    /// <param name="content">The detached content.</param>
    /// <param name="detached">Whether the content is detached rather than encapsulated.</param>
    /// <param name="certificate">The signer's certificate, or <see langword="null"/>.</param>
    /// <param name="extra">Extra certificates.</param>
    /// <returns>The result.</returns>
    private static CmsSignerCheck Check(ReadOnlySpan<byte> der, SignerInfo signer, ReadOnlySpan<byte> content, bool detached, X509Certificate2? certificate, X509Certificate2Collection extra)
    {
        ArgumentNullException.ThrowIfNull(signer);
        var oid = signer.DigestAlgorithm.Value;
        var algorithm = CmsDigests.FromOid(oid);
        var status = CmsDigests.Status(algorithm);
        var isShake = CmsDigests.IsShake(oid);
        if (!isShake && status != CmsDigestStatus.Available)
        {
            return new(algorithm, status, CmsSignatureStatus.Unsupported);
        }

        var parsed = CmsSignerReader.TryRead(der, out var layout);
        if (isShake)
        {
            if (!parsed)
            {
                return new(algorithm, status, CmsSignatureStatus.Invalid);
            }

            status = CmsDigests.ReadIdentifier(der[layout.DigestAlgorithm], out algorithm);
            if (status != CmsDigestStatus.Available)
            {
                return new(algorithm, status, CmsSignatureStatus.Unsupported);
            }
        }

        if (certificate is null || !parsed)
        {
            return new(algorithm, status, isShake ? CmsSignatureStatus.Invalid : CheckSignedCms(signer, extra));
        }

        var signed = detached ? content : der[layout.Content];
        return new(algorithm, status, CheckSignature(der, layout, signed, algorithm, certificate, signer, extra));
    }

    /// <summary>
    /// Checks the ISO/TS 32002 rules for the signer's curve and digest, then its signature: EdDSA and SHAKE256 signatures
    /// here, and the others with <see cref="SignedCms"/>.
    /// </summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where the first signer's parts are.</param>
    /// <param name="signed">The signed content.</param>
    /// <param name="digest">The signer's digest algorithm.</param>
    /// <param name="certificate">The signer's certificate.</param>
    /// <param name="signer">The first signer.</param>
    /// <param name="extra">Extra certificates.</param>
    /// <returns>The result.</returns>
    private static CmsSignatureStatus CheckSignature(
        ReadOnlySpan<byte> der,
        in CmsSignerLayout layout,
        ReadOnlySpan<byte> signed,
        CmsDigestAlgorithm digest,
        X509Certificate2 certificate,
        SignerInfo signer,
        X509Certificate2Collection extra)
    {
        var rules = CmsEcRules.Check(der, layout, digest, certificate);
        if (rules != CmsSignatureStatus.Valid)
        {
            return rules;
        }

        var kind = CmsSignatureAlgorithms.Read(der[layout.SignatureAlgorithm]).Kind;
        if (kind is CmsSignatureKind.Ed25519 or CmsSignatureKind.Ed448)
        {
            return CmsEdDsaSignatures.Verify(der, layout, signed, certificate);
        }

        return digest == CmsDigestAlgorithm.Shake256 ? CmsShakeSignatures.Verify(der, layout, signed, certificate) : CheckSignedCms(signer, extra);
    }

    /// <summary>Checks a signer with <see cref="SignedCms"/>, without judging the certificate.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="extra">Extra certificates to find the signer's in.</param>
    /// <returns>The result.</returns>
    private static CmsSignatureStatus CheckSignedCms(SignerInfo signer, X509Certificate2Collection extra)
    {
        try
        {
            signer.CheckSignature(extra, true);
            return CmsSignatureStatus.Valid;
        }
        catch (CryptographicException)
        {
            return CmsSignatureStatus.Invalid;
        }
    }
}
