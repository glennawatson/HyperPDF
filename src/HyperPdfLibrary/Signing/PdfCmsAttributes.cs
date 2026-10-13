// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Builds the signed attributes of a PDF signature's CMS container.</summary>
internal static class PdfCmsAttributes
{
    /// <summary>The levels of nesting the attributes need.</summary>
    private const int MaxDepth = 12;

    /// <summary>The bytes the writer starts with: the attributes of a signature are well under this.</summary>
    private const int InitialCapacity = 1024;

    /// <summary>The tag of the directoryName choice of a GeneralName: <c>[4]</c>, constructed.</summary>
    private const byte DirectoryNameTag = DerWriter.ContextConstructedTag + 4;

    /// <summary>Gets the encoded id-data object identifier, 1.2.840.113549.1.7.1, the content type of a detached PDF signature.</summary>
    internal static ReadOnlySpan<byte> DataOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01];

    /// <summary>Gets the encoded contentType attribute, 1.2.840.113549.1.9.3 (RFC 5652, 11.1).</summary>
    private static ReadOnlySpan<byte> ContentTypeOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x03];

    /// <summary>Gets the encoded messageDigest attribute, 1.2.840.113549.1.9.4 (RFC 5652, 11.2).</summary>
    private static ReadOnlySpan<byte> MessageDigestOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x04];

    /// <summary>Gets the encoded signingTime attribute, 1.2.840.113549.1.9.5 (RFC 5652, 11.3).</summary>
    private static ReadOnlySpan<byte> SigningTimeOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x05];

    /// <summary>Gets the encoded id-aa-signingCertificateV2 attribute, 1.2.840.113549.1.9.16.2.47 (RFC 5035).</summary>
    private static ReadOnlySpan<byte> SigningCertificateV2Oid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x2F];

    /// <summary>Builds the signed attributes as a DER SET, the bytes the signature covers (RFC 5652, 5.4).</summary>
    /// <param name="spec">What the container is made of.</param>
    /// <param name="messageDigest">The digest of the signed bytes.</param>
    /// <returns>The DER-encoded SET OF attributes, sorted as DER requires.</returns>
    internal static byte[] Build(in PdfCmsSpec spec, ReadOnlySpan<byte> messageDigest)
    {
        var writer = new DerWriter(stackalloc int[MaxDepth], InitialCapacity);
        try
        {
            writer.Begin(DerWriter.SetTag);
            writer.Begin(DerWriter.SequenceTag);
            writer.WriteObjectIdentifier(ContentTypeOid);
            writer.Begin(DerWriter.SetTag);
            writer.WriteObjectIdentifier(DataOid);
            writer.End();
            writer.End();

            writer.Begin(DerWriter.SequenceTag);
            writer.WriteObjectIdentifier(MessageDigestOid);
            writer.Begin(DerWriter.SetTag);
            writer.WriteOctetString(messageDigest);
            writer.End();
            writer.End();

            // ETSI EN 319 142-1, 6.3 g): when the dictionary has /M, a PDF 2.0 CAdES signature has no signing-time attribute, and
            // a PDF 1.x one should have none. adbe.pkcs7.detached keeps it (ISO 32000-2, 12.8.3.3).
            if (spec.Format != PdfSignatureFormat.CadesDetached)
            {
                WriteSigningTime(ref writer, spec.SigningTime);
            }

            WriteSigningCertificate(ref writer, spec);
            writer.EndSetOf();
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the signingTime attribute.</summary>
    /// <param name="writer">The writer, inside the attribute set.</param>
    /// <param name="time">The signing time.</param>
    private static void WriteSigningTime(ref DerWriter writer, DateTimeOffset time)
    {
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(SigningTimeOid);
        writer.Begin(DerWriter.SetTag);
        writer.WriteUtcTime(time);
        writer.End();
        writer.End();
    }

    /// <summary>Writes the signing-certificate-v2 attribute (RFC 5035) binding the signature to the signing certificate.</summary>
    /// <param name="writer">The writer, inside the attribute set.</param>
    /// <param name="spec">What the container is made of.</param>
    private static void WriteSigningCertificate(ref DerWriter writer, in PdfCmsSpec spec)
    {
        var certificate = PdfSignerCertificate.Read(spec.Certificates[0]);
        var hash = CertificateHash(spec.Digest);
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(SigningCertificateV2Oid);
        writer.Begin(DerWriter.SetTag);
        writer.Begin(DerWriter.SequenceTag);
        writer.Begin(DerWriter.SequenceTag);
        WriteCertificateId(ref writer, certificate, hash);
        writer.End();
        writer.End();
        writer.End();
        writer.End();
    }

    /// <summary>Writes an ESSCertIDv2: the hash algorithm (omitted for SHA-256, its default), the certificate's hash and its issuer and serial.</summary>
    /// <param name="writer">The writer, inside the SEQUENCE OF.</param>
    /// <param name="certificate">The signing certificate.</param>
    /// <param name="hash">The digest the certificate's hash uses.</param>
    private static void WriteCertificateId(ref DerWriter writer, in PdfSignerCertificate certificate, CmsDigestAlgorithm hash)
    {
        writer.Begin(DerWriter.SequenceTag);
        if (hash != CmsDigestAlgorithm.Sha256)
        {
            PdfCmsAlgorithmIds.WriteDigest(ref writer, hash);
        }

        Span<byte> value = stackalloc byte[CmsDigests.MaxLength];
        var length = CmsDigests.Hash(hash, certificate.Der, value);
        writer.WriteOctetString(value[..length]);
        writer.Begin(DerWriter.SequenceTag);
        writer.Begin(DerWriter.SequenceTag);
        writer.Begin(DirectoryNameTag);
        writer.WriteEncoded(certificate.IssuerName);
        writer.End();
        writer.End();
        writer.WriteIntegerContents(certificate.SerialNumber);
        writer.End();
        writer.End();
    }

    /// <summary>Gets the digest the signing certificate's hash uses: the signature's digest when it is a SHA-2 one, otherwise SHA-256.</summary>
    /// <param name="digest">The signature's digest.</param>
    /// <returns>The certificate hash's digest.</returns>
    private static CmsDigestAlgorithm CertificateHash(CmsDigestAlgorithm digest) =>
        digest is CmsDigestAlgorithm.Sha384 or CmsDigestAlgorithm.Sha512 ? digest : CmsDigestAlgorithm.Sha256;
}
