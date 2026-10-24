// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Builds detached CMS signatures with any digest AlgorithmIdentifier, written apart from the library and the viewer's
/// signer, so tests can make the SHAKE variants ISO/TS 32001 allows and the ones it forbids, and RSASSA-PSS with SHAKE256
/// (RFC 8702, 3.2.1), which neither OpenSSL nor .NET can sign.
/// </summary>
internal static class ShakeTestSigner
{
    /// <summary>The id-shake256 OID.</summary>
    internal const string Shake256Oid = "2.16.840.1.101.3.4.2.12";

    /// <summary>The id-shake256-len OID.</summary>
    internal const string Shake256LenOid = "2.16.840.1.101.3.4.2.18";

    /// <summary>The id-shake128 OID.</summary>
    internal const string Shake128Oid = "2.16.840.1.101.3.4.2.11";

    /// <summary>The id-ecdsa-with-shake256 OID.</summary>
    internal const string EcdsaShake256Oid = "1.3.6.1.5.5.7.6.33";

    /// <summary>The id-RSASSA-PSS-SHAKE256 OID.</summary>
    internal const string RsaPssShake256Oid = "1.3.6.1.5.5.7.6.31";

    /// <summary>The SHAKE256 output length.</summary>
    internal const int Shake256Length = 64;

    /// <summary>How long created certificates are valid after the signing time, in years.</summary>
    private const int ValidYears = 100;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The zero bytes that start M'.</summary>
    private const int PssPadding = 8;

    /// <summary>The identifier byte of the implicit <c>[0]</c> the signed attributes are stored with.</summary>
    private const byte ImplicitSetTag = 0xA0;

    /// <summary>The PSS trailer.</summary>
    private const byte PssTrailer = 0xBC;

    /// <summary>The tag of the signed attributes and certificates.</summary>
    private static readonly Asn1Tag Context0 = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Encodes an AlgorithmIdentifier with no parameters.</summary>
    /// <param name="oid">The OID.</param>
    /// <returns>The DER.</returns>
    internal static byte[] Identifier(string oid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
        }

        return writer.Encode();
    }

    /// <summary>Encodes an AlgorithmIdentifier with an INTEGER parameter, such as id-shake256-len's output bits.</summary>
    /// <param name="oid">The OID.</param>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The DER.</returns>
    internal static byte[] Identifier(string oid, int parameter)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            writer.WriteInteger(parameter);
        }

        return writer.Encode();
    }

    /// <summary>Encodes an AlgorithmIdentifier with a NULL parameter.</summary>
    /// <param name="oid">The OID.</param>
    /// <returns>The DER.</returns>
    internal static byte[] IdentifierWithNull(string oid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            writer.WriteNull();
        }

        return writer.Encode();
    }

    /// <summary>Signs content with ECDSA over SHAKE256 of the signed attributes.</summary>
    /// <param name="content">The content.</param>
    /// <param name="certificate">The signer, with an ECDSA key.</param>
    /// <param name="digestAlgorithm">The digest AlgorithmIdentifier to record.</param>
    /// <returns>The DER ContentInfo.</returns>
    internal static byte[] SignEcdsa(byte[] content, X509Certificate2 certificate, byte[] digestAlgorithm)
    {
        using var key = certificate.GetECDsaPrivateKey()!;
        return Build(
            Shake256.HashData(content, Shake256Length),
            certificate,
            digestAlgorithm,
            Identifier(EcdsaShake256Oid),
            attributes => key.SignHash(Shake256.HashData(attributes, Shake256Length), DSASignatureFormat.Rfc3279DerSequence));
    }

    /// <summary>Signs content with RSASSA-PSS over SHAKE256 of the signed attributes.</summary>
    /// <param name="content">The content.</param>
    /// <param name="certificate">The signer, with an exportable RSA key.</param>
    /// <returns>The DER ContentInfo.</returns>
    internal static byte[] SignRsaPss(byte[] content, X509Certificate2 certificate)
    {
        using var key = certificate.GetRSAPrivateKey()!;
        var parameters = key.ExportParameters(true);
        return Build(
            Shake256.HashData(content, Shake256Length),
            certificate,
            Identifier(Shake256Oid),
            Identifier(RsaPssShake256Oid),
            attributes => PssSign(parameters, Shake256.HashData(attributes, Shake256Length)));
    }

    /// <summary>Signs content with any digest OID and a SHA-256 RSA signature, for digests the checker must refuse.</summary>
    /// <param name="content">The content.</param>
    /// <param name="certificate">The signer, with an RSA key.</param>
    /// <param name="digestOid">The digest OID to record.</param>
    /// <returns>The DER ContentInfo.</returns>
    internal static byte[] SignWithDigestOid(byte[] content, X509Certificate2 certificate, string digestOid)
    {
        using var key = certificate.GetRSAPrivateKey()!;

        // The digest is one the checker refuses, so the messageDigest value is never compared; SHA-256 keeps this signer free of SHAKE256.
        return Build(
            SHA256.HashData(content),
            certificate,
            Identifier(digestOid),
            IdentifierWithNull("1.2.840.113549.1.1.11"),
            attributes => key.SignData(attributes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    /// <summary>Creates a P-256 certificate.</summary>
    /// <param name="name">The common name.</param>
    /// <returns>The certificate with its key.</returns>
    internal static X509Certificate2 CreateEcdsaCertificate(string name)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(PdfSigning.SigningTime.AddDays(-1), PdfSigning.SigningTime.AddYears(ValidYears));
    }

    /// <summary>Lays out a detached SignedData with content-type, signing-time and message-digest attributes.</summary>
    /// <param name="messageDigest">The digest of the content to put in the message-digest attribute.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="digestAlgorithm">The digest AlgorithmIdentifier.</param>
    /// <param name="signatureAlgorithm">The signature AlgorithmIdentifier.</param>
    /// <param name="sign">Signs the DER SET OF signed attributes.</param>
    /// <returns>The DER ContentInfo.</returns>
    private static byte[] Build(byte[] messageDigest, X509Certificate2 certificate, byte[] digestAlgorithm, byte[] signatureAlgorithm, Func<byte[], byte[]> sign)
    {
        var attributes = Attributes(messageDigest);
        var signature = sign(attributes);
        var stored = attributes.AsSpan().ToArray();
        stored[0] = ImplicitSetTag;
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier("1.2.840.113549.1.7.2");
            using (writer.PushSequence(Context0))
            using (writer.PushSequence())
            {
                writer.WriteInteger(1);
                using (writer.PushSetOf())
                {
                    writer.WriteEncodedValue(digestAlgorithm);
                }

                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier("1.2.840.113549.1.7.1");
                }

                using (writer.PushSetOf(Context0))
                {
                    writer.WriteEncodedValue(certificate.RawData);
                }

                using (writer.PushSetOf())
                using (writer.PushSequence())
                {
                    writer.WriteInteger(1);
                    using (writer.PushSequence())
                    {
                        writer.WriteEncodedValue(certificate.IssuerName.RawData);
                        writer.WriteInteger(certificate.SerialNumberBytes.Span);
                    }

                    writer.WriteEncodedValue(digestAlgorithm);
                    writer.WriteEncodedValue(stored);
                    writer.WriteEncodedValue(signatureAlgorithm);
                    writer.WriteOctetString(signature);
                }
            }
        }

        return writer.Encode();
    }

    /// <summary>Encodes the signed attributes as a DER SET OF.</summary>
    /// <param name="digest">The message digest.</param>
    /// <returns>The DER.</returns>
    private static byte[] Attributes(byte[] digest)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.9.3");
                using (writer.PushSetOf())
                {
                    writer.WriteObjectIdentifier("1.2.840.113549.1.7.1");
                }
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.9.5");
                using (writer.PushSetOf())
                {
                    writer.WriteUtcTime(PdfSigning.SigningTime);
                }
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.9.4");
                using (writer.PushSetOf())
                {
                    writer.WriteOctetString(digest);
                }
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// EMSA-PSS-ENCODE (RFC 8017, 9.1.1) with SHAKE256 for the hash and the mask and a 64-byte salt, then the RSA private
    /// key operation on <see cref="BigInteger"/>; test code only.
    /// </summary>
    /// <param name="key">The private key.</param>
    /// <param name="hash">The 64-byte message hash.</param>
    /// <returns>The signature.</returns>
    private static byte[] PssSign(RSAParameters key, byte[] hash)
    {
        var modulus = new BigInteger(key.Modulus, true, true);
        var bits = (int)modulus.GetBitLength() - 1;
        var length = (bits + ByteBits - 1) / ByteBits;
        var salt = RandomNumberGenerator.GetBytes(Shake256Length);
        byte[] padded = [.. new byte[PssPadding], .. hash, .. salt];
        var h = Shake256.HashData(padded, Shake256Length);
        var block = new byte[length - Shake256Length - 1];
        block[block.Length - Shake256Length - 1] = 1;
        salt.CopyTo(block, block.Length - Shake256Length);
        var mask = Shake256.HashData(h, block.Length);
        for (var i = 0; i < block.Length; i++)
        {
            block[i] ^= mask[i];
        }

        block[0] &= (byte)(0xFF >> ((length * ByteBits) - bits));
        byte[] encoded = [.. block, .. h, PssTrailer];
        var signature = BigInteger.ModPow(new(encoded, true, true), new(key.D, true, true), modulus);
        var result = new byte[key.Modulus!.Length];
        var bytes = signature.ToByteArray(true, true);
        bytes.CopyTo(result, result.Length - bytes.Length);
        return result;
    }
}
