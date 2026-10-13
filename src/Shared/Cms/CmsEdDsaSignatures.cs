// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Checks a CMS signer whose signature is Ed25519 or Ed448, which <c>SignedCms</c> does not support. As RFC 8419, 3.1
/// and 3.2 specify, the message-digest attribute is SHA-512 (Ed25519) or the 512-bit SHAKE256 (Ed448) of the content,
/// and the signature covers the DER signed attributes, or the content itself when there are none; EdDSA does not hash
/// the message first as the signature algorithm.
/// </summary>
internal static class CmsEdDsaSignatures
{
    /// <summary>The SET OF tag a signed attributes value is signed with, in place of its <c>[0] IMPLICIT</c> tag.</summary>
    private const byte SetTag = 0x31;

    /// <summary>Checks the first signer of a container.</summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where its first signer's parts are.</param>
    /// <param name="content">The signed content: the detached bytes, or the encapsulated content.</param>
    /// <param name="certificate">The signer's certificate.</param>
    /// <returns>The result.</returns>
    internal static CmsSignatureStatus Verify(ReadOnlySpan<byte> der, in CmsSignerLayout layout, ReadOnlySpan<byte> content, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var kind = CmsSignatureAlgorithms.Read(der[layout.SignatureAlgorithm]).Kind;
        var curve = kind == CmsSignatureKind.Ed448 ? EdwardsCurve.Ed448 : EdwardsCurve.Ed25519;
        var digest = kind == CmsSignatureKind.Ed448 ? CmsDigestAlgorithm.Shake256 : CmsDigestAlgorithm.Sha512;
        var key = CmsCertificateKey.Read(certificate).PublicKey;
        var attributes = der[layout.SignedAttributes];
        var signature = der[layout.Signature];
        if (attributes.IsEmpty)
        {
            return ToStatus(EdDsa.Verify(curve, key, content, signature));
        }

        if (!MessageDigestMatches(digest, attributes, content))
        {
            return CmsSignatureStatus.Invalid;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(attributes.Length);
        try
        {
            var signed = buffer.AsSpan(0, attributes.Length);
            attributes.CopyTo(signed);
            signed[0] = SetTag;
            return ToStatus(EdDsa.Verify(curve, key, signed, signature));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Maps a verification result to a status.</summary>
    /// <param name="valid">Whether the signature is good.</param>
    /// <returns>The status.</returns>
    private static CmsSignatureStatus ToStatus(bool valid) => valid ? CmsSignatureStatus.Valid : CmsSignatureStatus.Invalid;

    /// <summary>Determines whether the message-digest attribute is the digest of the content.</summary>
    /// <param name="digest">The digest Table 2 fixes for the curve.</param>
    /// <param name="attributes">The whole signed attributes value.</param>
    /// <param name="content">The content.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool MessageDigestMatches(CmsDigestAlgorithm digest, ReadOnlySpan<byte> attributes, ReadOnlySpan<byte> content)
    {
        if (!CmsShakeSignatures.TryFindMessageDigest(attributes, out var expected))
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[CmsDigests.MaxLength];
        var length = CmsDigests.Hash(digest, content, actual);
        return length > 0 && CryptographicOperations.FixedTimeEquals(actual[..length], expected);
    }
}
