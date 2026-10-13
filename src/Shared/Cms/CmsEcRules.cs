// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Checks a signer against the rules of ISO/TS 32002:2022 before its signature is: named curves only and the permitted
/// curve and digest pairs (5.1.3, Tables 3 and 4), Ed25519 with SHA-512 and Ed448 with the fixed-length id-shake256
/// (5.1.2, Table 2), and one digest for the content and the signed attributes (5.1.4).
/// </summary>
internal static class CmsEcRules
{
    /// <summary>Checks a signer.</summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where the signer's parts are.</param>
    /// <param name="digest">The signer's digest algorithm.</param>
    /// <param name="certificate">The signer's certificate.</param>
    /// <returns>
    /// <see cref="CmsSignatureStatus.Valid"/> when no rule is broken and the signature can be checked; otherwise the rule
    /// broken, or <see cref="CmsSignatureStatus.Unsupported"/> when the curve is unlisted or this computer lacks it.
    /// </returns>
    internal static CmsSignatureStatus Check(ReadOnlySpan<byte> der, in CmsSignerLayout layout, CmsDigestAlgorithm digest, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var algorithm = CmsSignatureAlgorithms.Read(der[layout.SignatureAlgorithm]);
        var key = CmsCertificateKey.Read(certificate);
        var signatureIsEdwards = algorithm.Kind is CmsSignatureKind.Ed25519 or CmsSignatureKind.Ed448;
        if (signatureIsEdwards || key.IsEdwards)
        {
            return CheckEdDsa(der, layout, digest, algorithm.Kind, key);
        }

        return algorithm.Kind == CmsSignatureKind.Ecdsa && key.IsEllipticCurve
            ? CheckEcdsa(der, layout, digest, algorithm, key)
            : CmsSignatureStatus.Valid;
    }

    /// <summary>Determines whether a certificate holds an elliptic-curve key, whose signatures ISO/TS 32002 covers.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> for an id-ecPublicKey key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEllipticCurveKey(X509Certificate2 certificate) => CmsCertificateKey.Read(certificate).IsEllipticCurve;

    /// <summary>Checks whether a certificate may sign with a digest under ISO/TS 32002 (5.1.3, Table 3).</summary>
    /// <param name="certificate">The signer's certificate.</param>
    /// <param name="digest">The digest to sign with.</param>
    /// <returns><see cref="CmsSignatureStatus.Valid"/> for a key that is not an elliptic-curve key or a permitted pair; otherwise the rule broken.</returns>
    internal static CmsSignatureStatus CheckSigner(X509Certificate2 certificate, CmsDigestAlgorithm digest)
    {
        var key = CmsCertificateKey.Read(certificate);
        return key.IsEllipticCurve ? CheckCurve(key.Parameters, digest, CmsDigestAlgorithm.Unknown) : CmsSignatureStatus.Valid;
    }

    /// <summary>Checks whether an elliptic-curve key may sign with a digest under ISO/TS 32002 (5.1.3, Table 3).</summary>
    /// <param name="keyParameters">The encoded parameters of the key's id-ecPublicKey algorithm: the named curve.</param>
    /// <param name="digest">The digest to sign with.</param>
    /// <returns><see cref="CmsSignatureStatus.Valid"/> for a permitted pair; otherwise the rule broken.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CmsSignatureStatus CheckCurveSigner(ReadOnlySpan<byte> keyParameters, CmsDigestAlgorithm digest) =>
        CheckCurve(keyParameters, digest, CmsDigestAlgorithm.Unknown);

    /// <summary>Checks an EdDSA signer: the key matches the algorithm and the digest is the one Table 2 fixes.</summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where the signer's parts are.</param>
    /// <param name="digest">The signer's digest algorithm.</param>
    /// <param name="kind">The signature algorithm the signer names.</param>
    /// <param name="key">The certificate's key.</param>
    /// <returns>The result.</returns>
    private static CmsSignatureStatus CheckEdDsa(ReadOnlySpan<byte> der, in CmsSignerLayout layout, CmsDigestAlgorithm digest, CmsSignatureKind kind, in CmsCertificateKey key)
    {
        var matches = (kind == CmsSignatureKind.Ed25519 && key.IsEd25519) || (kind == CmsSignatureKind.Ed448 && key.IsEd448);
        if (!matches)
        {
            return CmsSignatureStatus.Invalid;
        }

        if (kind == CmsSignatureKind.Ed25519)
        {
            return digest == CmsDigestAlgorithm.Sha512 ? CmsSignatureStatus.Valid : CmsSignatureStatus.CurveDigestNotAllowed;
        }

        return digest == CmsDigestAlgorithm.Shake256 && CmsDigests.IsFixedShake256(der[layout.DigestAlgorithm])
            ? CmsSignatureStatus.Valid
            : CmsSignatureStatus.CurveDigestNotAllowed;
    }

    /// <summary>Checks an ECDSA signer's curve, digest pair and digest congruence.</summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where the signer's parts are.</param>
    /// <param name="digest">The signer's digest algorithm.</param>
    /// <param name="algorithm">The signature algorithm the signer names.</param>
    /// <param name="key">The signer's elliptic-curve key.</param>
    /// <returns>The result.</returns>
    private static CmsSignatureStatus CheckEcdsa(ReadOnlySpan<byte> der, in CmsSignerLayout layout, CmsDigestAlgorithm digest, CmsSignatureAlgorithm algorithm, in CmsCertificateKey key)
    {
        var hasAttributes = !der[layout.SignedAttributes].IsEmpty;
        return CheckCurve(key.Parameters, digest, hasAttributes ? algorithm.Digest : CmsDigestAlgorithm.Unknown);
    }

    /// <summary>Checks a key's curve against a digest, and against the digest the signature algorithm names.</summary>
    /// <param name="parameters">The encoded parameters of the key algorithm, or empty or <see langword="null"/> when there are none.</param>
    /// <param name="digest">The signer's digest algorithm.</param>
    /// <param name="signedAttributesDigest">The digest the signature algorithm hashes the signed attributes with, or unknown when none applies.</param>
    /// <returns>The result.</returns>
    private static CmsSignatureStatus CheckCurve(ReadOnlySpan<byte> parameters, CmsDigestAlgorithm digest, CmsDigestAlgorithm signedAttributesDigest)
    {
        if (!TryReadNamedCurve(parameters, out var curve))
        {
            return CmsSignatureStatus.UnnamedCurve;
        }

        if (curve == CmsEcCurve.Other)
        {
            return CmsSignatureStatus.Unsupported;
        }

        if (signedAttributesDigest != CmsDigestAlgorithm.Unknown && signedAttributesDigest != digest)
        {
            return CmsSignatureStatus.DigestMismatch;
        }

        if (!CmsEcCurves.Allows(curve, digest))
        {
            return CmsSignatureStatus.CurveDigestNotAllowed;
        }

        return CmsEcCurves.IsSupported(curve) ? CmsSignatureStatus.Valid : CmsSignatureStatus.Unsupported;
    }

    /// <summary>Reads the curve from the certificate's key parameters, which must be a namedCurve OID (RFC 5480, 2.1.1).</summary>
    /// <param name="parameters">The encoded parameters of the key algorithm.</param>
    /// <param name="curve">The curve.</param>
    /// <returns><see langword="false"/> when the parameters are explicit, implicit or absent.</returns>
    private static bool TryReadNamedCurve(ReadOnlySpan<byte> parameters, out CmsEcCurve curve)
    {
        curve = CmsEcCurve.Other;
        if (!CmsDer.TryRead(parameters, out var tag, out var oid, out _) || tag != Asn1Tag.ObjectIdentifier)
        {
            return false;
        }

        curve = CmsEcCurves.FromEncoded(oid);
        return true;
    }
}
