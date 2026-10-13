// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Signing;

/// <summary>Writes the AlgorithmIdentifier structures of a CMS signature container.</summary>
internal static class PdfCmsAlgorithmIds
{
    /// <summary>The tag of the PSS hash algorithm field: <c>[0]</c>, constructed.</summary>
    private const byte HashFieldTag = DerWriter.ContextConstructedTag;

    /// <summary>The tag of the PSS mask generation field: <c>[1]</c>, constructed.</summary>
    private const byte MaskFieldTag = DerWriter.ContextConstructedTag + 1;

    /// <summary>The tag of the PSS salt length field: <c>[2]</c>, constructed.</summary>
    private const byte SaltFieldTag = DerWriter.ContextConstructedTag + 2;

    /// <summary>Gets the encoded rsaEncryption object identifier, 1.2.840.113549.1.1.1 (RFC 8017).</summary>
    private static ReadOnlySpan<byte> RsaOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01];

    /// <summary>Gets the encoded id-RSASSA-PSS object identifier, 1.2.840.113549.1.1.10 (RFC 4055).</summary>
    private static ReadOnlySpan<byte> PssOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0A];

    /// <summary>Gets the encoded id-mgf1 object identifier, 1.2.840.113549.1.1.8 (RFC 4055).</summary>
    private static ReadOnlySpan<byte> Mgf1Oid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x08];

    /// <summary>Gets the encoded ecdsa-with-SHA256 object identifier, 1.2.840.10045.4.3.2 (RFC 5758).</summary>
    private static ReadOnlySpan<byte> EcdsaSha256Oid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x02];

    /// <summary>Gets the encoded ecdsa-with-SHA384 object identifier, 1.2.840.10045.4.3.3 (RFC 5758).</summary>
    private static ReadOnlySpan<byte> EcdsaSha384Oid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x03];

    /// <summary>Gets the encoded ecdsa-with-SHA512 object identifier, 1.2.840.10045.4.3.4 (RFC 5758).</summary>
    private static ReadOnlySpan<byte> EcdsaSha512Oid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x04];

    /// <summary>Gets the encoded id-ecdsa-with-sha3-256 object identifier, 2.16.840.1.101.3.4.3.10 (RFC 9688).</summary>
    private static ReadOnlySpan<byte> EcdsaSha3_256Oid => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x03, 0x0A];

    /// <summary>Gets the encoded id-ecdsa-with-sha3-384 object identifier, 2.16.840.1.101.3.4.3.11 (RFC 9688).</summary>
    private static ReadOnlySpan<byte> EcdsaSha3_384Oid => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x03, 0x0B];

    /// <summary>Gets the encoded id-ecdsa-with-sha3-512 object identifier, 2.16.840.1.101.3.4.3.12 (RFC 9688).</summary>
    private static ReadOnlySpan<byte> EcdsaSha3_512Oid => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x03, 0x0C];

    /// <summary>Gets the encoded id-ecdsa-with-shake256 object identifier, 1.3.6.1.5.5.7.6.33 (RFC 8702).</summary>
    private static ReadOnlySpan<byte> EcdsaShake256Oid => [0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x06, 0x21];

    /// <summary>Writes a digest AlgorithmIdentifier: the object identifier, with no parameters (RFC 5754).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digest">The digest.</param>
    /// <exception cref="NotSupportedException">The digest has no object identifier.</exception>
    internal static void WriteDigest(ref DerWriter writer, CmsDigestAlgorithm digest)
    {
        Span<byte> oid = stackalloc byte[CmsDigests.MaxEncodedOidLength];
        var length = CmsDigests.EncodeOid(digest, oid);
        if (length == 0)
        {
            throw new NotSupportedException("This digest has no object identifier.");
        }

        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(oid[..length]);
        writer.End();
    }

    /// <summary>Writes the AlgorithmIdentifier of a signature.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="algorithm">The kind of signature.</param>
    /// <param name="digest">The digest.</param>
    /// <exception cref="NotSupportedException">The pair has no algorithm identifier.</exception>
    internal static void WriteSignature(ref DerWriter writer, PdfSignatureAlgorithm algorithm, CmsDigestAlgorithm digest)
    {
        switch (algorithm)
        {
            case PdfSignatureAlgorithm.RsaPkcs1:
            {
                writer.Begin(DerWriter.SequenceTag);
                writer.WriteObjectIdentifier(RsaOid);
                writer.WriteNull();
                writer.End();
                break;
            }

            case PdfSignatureAlgorithm.RsaPss:
            {
                WritePss(ref writer, digest);
                break;
            }

            default:
            {
                writer.Begin(DerWriter.SequenceTag);
                writer.WriteObjectIdentifier(EcdsaOid(digest));
                writer.End();
                break;
            }
        }
    }

    /// <summary>Gets the encoded object identifier of ECDSA with a digest.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The identifier's contents.</returns>
    /// <exception cref="NotSupportedException">The digest has no ECDSA identifier.</exception>
    private static ReadOnlySpan<byte> EcdsaOid(CmsDigestAlgorithm digest) => digest switch
    {
        CmsDigestAlgorithm.Sha256 => EcdsaSha256Oid,
        CmsDigestAlgorithm.Sha384 => EcdsaSha384Oid,
        CmsDigestAlgorithm.Sha512 => EcdsaSha512Oid,
        CmsDigestAlgorithm.Sha3_256 => EcdsaSha3_256Oid,
        CmsDigestAlgorithm.Sha3_384 => EcdsaSha3_384Oid,
        CmsDigestAlgorithm.Sha3_512 => EcdsaSha3_512Oid,
        CmsDigestAlgorithm.Shake256 => EcdsaShake256Oid,
        _ => throw new NotSupportedException("This digest cannot be used with an elliptic-curve signature."),
    };

    /// <summary>Writes the RSASSA-PSS AlgorithmIdentifier with the digest as both the hash and the mask function's hash, and a salt as long as the digest.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digest">The digest.</param>
    private static void WritePss(ref DerWriter writer, CmsDigestAlgorithm digest)
    {
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(PssOid);
        writer.Begin(DerWriter.SequenceTag);
        writer.Begin(HashFieldTag);
        WriteDigest(ref writer, digest);
        writer.End();
        writer.Begin(MaskFieldTag);
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(Mgf1Oid);
        WriteDigest(ref writer, digest);
        writer.End();
        writer.End();
        writer.Begin(SaltFieldTag);
        writer.WriteInteger(CmsDigests.Length(digest));
        writer.End();
        writer.End();
        writer.End();
    }
}
