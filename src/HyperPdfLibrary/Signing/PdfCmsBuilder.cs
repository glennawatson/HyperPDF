// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Signing;

/// <summary>Assembles the CMS SignedData container (RFC 5652) a PDF signature holds in its /Contents.</summary>
internal static class PdfCmsBuilder
{
    /// <summary>The SignedData and SignerInfo version when the signer is named by issuer and serial number.</summary>
    private const int Version = 1;

    /// <summary>The levels of nesting the container needs.</summary>
    private const int MaxDepth = 16;

    /// <summary>The bytes of the container that do not depend on the certificates, signed attributes, signature or token.</summary>
    private const int Overhead = 256;

    /// <summary>The tag of the signed attributes as they are signed: a SET OF.</summary>
    private const byte SignedSetTag = DerWriter.SetTag;

    /// <summary>The tag of the signed attributes as they are stored: the context tag [0], constructed.</summary>
    private const byte StoredAttributesTag = DerWriter.ContextConstructedTag;

    /// <summary>The tag of the unsigned attributes: <c>[1]</c>, constructed.</summary>
    private const byte UnsignedAttributesTag = DerWriter.ContextConstructedTag + 1;

    /// <summary>Gets the encoded id-signedData object identifier, 1.2.840.113549.1.7.2.</summary>
    private static ReadOnlySpan<byte> SignedDataOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x02];

    /// <summary>Gets the encoded signatureTimeStampToken unsigned attribute, 1.2.840.113549.1.9.16.2.14 (RFC 3161 and ETSI EN 319 122-1).</summary>
    private static ReadOnlySpan<byte> TimestampTokenOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x02, 0x0E];

    /// <summary>Assembles a SignedData container around a signature already made over the signed attributes.</summary>
    /// <param name="spec">What the container is made of.</param>
    /// <param name="signedAttributes">The DER SET of signed attributes that was signed.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="timestampToken">A DER timestamp token to add as the signature timestamp unsigned attribute, or empty for none.</param>
    /// <returns>The DER-encoded ContentInfo.</returns>
    /// <exception cref="ArgumentException">The signed attributes are not a DER SET.</exception>
    internal static byte[] Assemble(in PdfCmsSpec spec, ReadOnlySpan<byte> signedAttributes, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> timestampToken)
    {
        var signer = PdfSignerCertificate.Read(spec.Certificates[0]);
        var capacity = Overhead + signedAttributes.Length + signature.Length + timestampToken.Length;
        foreach (var certificate in spec.Certificates)
        {
            capacity += certificate.Length;
        }

        var writer = new DerWriter(stackalloc int[MaxDepth], capacity);
        try
        {
            writer.Begin(DerWriter.SequenceTag);
            writer.WriteObjectIdentifier(SignedDataOid);
            writer.Begin(StoredAttributesTag);
            WriteSignedData(ref writer, spec, signer, signedAttributes, signature, timestampToken);
            writer.End();
            writer.End();
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the SignedData sequence.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="spec">What the container is made of.</param>
    /// <param name="signer">The signing certificate.</param>
    /// <param name="signedAttributes">The signed attributes.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="timestampToken">The timestamp token, or empty.</param>
    private static void WriteSignedData(
        ref DerWriter writer,
        in PdfCmsSpec spec,
        in PdfSignerCertificate signer,
        ReadOnlySpan<byte> signedAttributes,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> timestampToken)
    {
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteInteger(Version);
        writer.Begin(DerWriter.SetTag);
        PdfCmsAlgorithmIds.WriteDigest(ref writer, spec.Digest);
        writer.End();

        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(PdfCmsAttributes.DataOid);
        writer.End();

        writer.Begin(StoredAttributesTag);
        foreach (var certificate in spec.Certificates)
        {
            writer.WriteEncoded(certificate);
        }

        writer.EndSetOf();

        writer.Begin(DerWriter.SetTag);
        WriteSignerInfo(ref writer, spec, signer, signedAttributes, signature, timestampToken);
        writer.End();
        writer.End();
    }

    /// <summary>Writes the SignerInfo sequence.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="spec">What the container is made of.</param>
    /// <param name="signer">The signing certificate.</param>
    /// <param name="signedAttributes">The signed attributes.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="timestampToken">The timestamp token, or empty.</param>
    /// <exception cref="ArgumentException">The signed attributes are not a DER SET.</exception>
    private static void WriteSignerInfo(
        ref DerWriter writer,
        in PdfCmsSpec spec,
        in PdfSignerCertificate signer,
        ReadOnlySpan<byte> signedAttributes,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> timestampToken)
    {
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteInteger(Version);
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteEncoded(signer.IssuerName);
        writer.WriteIntegerContents(signer.SerialNumber);
        writer.End();

        PdfCmsAlgorithmIds.WriteDigest(ref writer, spec.Digest);

        // The signed attributes are stored with the [0] tag instead of the SET tag they were signed with (RFC 5652, 5.4).
        if (signedAttributes.IsEmpty || signedAttributes[0] != SignedSetTag)
        {
            throw new ArgumentException("The signed attributes are not a DER SET.", nameof(signedAttributes));
        }

        writer.WriteEncoded(signedAttributes, StoredAttributesTag);
        PdfCmsAlgorithmIds.WriteSignature(ref writer, spec.Algorithm, spec.Digest);
        writer.WriteOctetString(signature);
        if (!timestampToken.IsEmpty)
        {
            WriteTimestamp(ref writer, timestampToken);
        }

        writer.End();
    }

    /// <summary>Writes the signature timestamp as the signer's only unsigned attribute.</summary>
    /// <param name="writer">The writer, inside the SignerInfo.</param>
    /// <param name="token">The DER timestamp token.</param>
    private static void WriteTimestamp(ref DerWriter writer, ReadOnlySpan<byte> token)
    {
        writer.Begin(UnsignedAttributesTag);
        writer.Begin(DerWriter.SequenceTag);
        writer.WriteObjectIdentifier(TimestampTokenOid);
        writer.Begin(DerWriter.SetTag);
        writer.WriteEncoded(token);
        writer.End();
        writer.End();
        writer.End();
    }
}
