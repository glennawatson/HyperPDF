// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// The key of a certificate, read in place from its DER bytes (RFC 5280, 4.1): the key algorithm, its parameters and the
/// public key bits. Reading it makes no objects, where <c>X509Certificate2.PublicKey</c> builds a key object, an OID and
/// copies of the encoded bytes on each use. The spans point into the certificate, so they are good while it is.
/// </summary>
/// <param name="algorithm">The key algorithm OID's content bytes.</param>
/// <param name="parameters">The whole encoded parameters, tag and length included, or empty when there are none.</param>
/// <param name="publicKey">The public key bytes, without the BIT STRING's unused-bits byte.</param>
internal readonly ref struct CmsCertificateKey(ReadOnlySpan<byte> algorithm, ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> publicKey)
{
    /// <summary>How many fields of the to-be-signed certificate come before the public key info: serial, signature, issuer, validity, subject.</summary>
    private const int FieldsBeforeKey = 5;

    /// <summary>Gets the key algorithm OID's content bytes; empty when the certificate could not be read.</summary>
    internal ReadOnlySpan<byte> Algorithm { get; } = algorithm;

    /// <summary>Gets the whole encoded algorithm parameters, or empty when there are none.</summary>
    internal ReadOnlySpan<byte> Parameters { get; } = parameters;

    /// <summary>Gets the public key bytes: for an Ed25519 or Ed448 key, the encoded point.</summary>
    internal ReadOnlySpan<byte> PublicKey { get; } = publicKey;

    /// <summary>Gets a value indicating whether the key is an elliptic-curve key (id-ecPublicKey).</summary>
    internal bool IsEllipticCurve => Algorithm.SequenceEqual(EcPublicKeyOid);

    /// <summary>Gets a value indicating whether the key is an Ed25519 key.</summary>
    internal bool IsEd25519 => Algorithm.SequenceEqual(Ed25519Oid);

    /// <summary>Gets a value indicating whether the key is an Ed448 key.</summary>
    internal bool IsEd448 => Algorithm.SequenceEqual(Ed448Oid);

    /// <summary>Gets a value indicating whether the key is an Ed25519 or Ed448 key.</summary>
    internal bool IsEdwards => IsEd25519 || IsEd448;

    /// <summary>Gets the id-ecPublicKey key algorithm OID (RFC 5480, 2.1.1), 1.2.840.10045.2.1.</summary>
    private static ReadOnlySpan<byte> EcPublicKeyOid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01];

    /// <summary>Gets the id-Ed25519 key algorithm OID (RFC 8410, 3), 1.3.101.112.</summary>
    private static ReadOnlySpan<byte> Ed25519Oid => [0x2B, 0x65, 0x70];

    /// <summary>Gets the id-Ed448 key algorithm OID (RFC 8410, 3), 1.3.101.113.</summary>
    private static ReadOnlySpan<byte> Ed448Oid => [0x2B, 0x65, 0x71];

    /// <summary>Reads the key of a certificate.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The key; with an empty algorithm when the certificate's DER cannot be read.</returns>
    internal static CmsCertificateKey Read(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return TryReadKeyInfo(certificate.RawDataMemory.Span, out var info) && TryReadKey(info, out var key) ? key : default;
    }

    /// <summary>Finds the SubjectPublicKeyInfo of a certificate.</summary>
    /// <param name="der">The certificate's DER bytes.</param>
    /// <param name="info">Receives the contents of the SubjectPublicKeyInfo.</param>
    /// <returns><see langword="true"/> when it was found.</returns>
    private static bool TryReadKeyInfo(ReadOnlySpan<byte> der, out ReadOnlySpan<byte> info)
    {
        info = default;
        if (!CmsDer.TryRead(der, out _, out var certificate, out _) || !CmsDer.TryRead(certificate, out _, out var tbs, out _))
        {
            return false;
        }

        // The version is optional: without it, the first field is the serial number.
        var at = 0;
        if (!CmsDer.TryNext(tbs, ref at, out var first, out _, out _))
        {
            return false;
        }

        var skip = first.TagClass == TagClass.ContextSpecific && first.TagValue == 0 ? FieldsBeforeKey : FieldsBeforeKey - 1;
        for (var i = 0; i < skip; i++)
        {
            if (!CmsDer.TryNext(tbs, ref at, out _, out _, out _))
            {
                return false;
            }
        }

        return CmsDer.TryNext(tbs, ref at, out _, out _, out info);
    }

    /// <summary>Reads the algorithm, parameters and key bits from a SubjectPublicKeyInfo.</summary>
    /// <param name="info">The contents of the SubjectPublicKeyInfo.</param>
    /// <param name="key">Receives the key.</param>
    /// <returns><see langword="true"/> when it was read.</returns>
    private static bool TryReadKey(ReadOnlySpan<byte> info, out CmsCertificateKey key)
    {
        key = default;
        var at = 0;
        if (!CmsDer.TryNext(info, ref at, out _, out _, out var identifier) || !CmsDer.TryNext(info, ref at, out _, out _, out var bits) || bits.IsEmpty)
        {
            return false;
        }

        var inner = 0;
        if (!CmsDer.TryNext(identifier, ref inner, out _, out _, out var oid))
        {
            return false;
        }

        // The parameters are optional; they are kept whole, as an encoded value.
        var parametersAt = inner;
        var parameters = CmsDer.TryNext(identifier, ref inner, out _, out _, out _) ? identifier[parametersAt..inner] : default;

        // The first byte of a BIT STRING counts its unused bits.
        key = new(oid, parameters, bits[1..]);
        return true;
    }
}
