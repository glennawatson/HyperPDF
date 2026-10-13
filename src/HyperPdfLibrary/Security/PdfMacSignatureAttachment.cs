// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Attaches a PDF MAC token to a signature as an unsigned attribute of its first SignerInfo (ISO/TS 32004:2024 6.5.2).
/// Unsigned attributes are outside the signature, so attaching one leaves the signed bytes and the signature value untouched.
/// </summary>
internal static class PdfMacSignatureAttachment
{
    /// <summary>The SignedData fields before the optional certificates: the version, the digest algorithms and the encapsulated content.</summary>
    private const int LeadingFields = 3;

    /// <summary>Attaches a token to the first signer of a signature container.</summary>
    /// <param name="signedData">The CMS ContentInfo holding the SignedData.</param>
    /// <param name="token">The DER PDF MAC token.</param>
    /// <returns>The container with the token as an unsigned attribute, with every other byte of the signed data and signature unchanged.</returns>
    /// <exception cref="AsnContentException">The container is not a SignedData with a signer.</exception>
    internal static byte[] Attach(ReadOnlySpan<byte> signedData, ReadOnlySpan<byte> token)
    {
        var reader = new AsnReader(signedData.ToArray(), AsnEncodingRules.BER);
        var info = reader.ReadSequence();
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(info.ReadEncodedValue().Span);
            using (writer.PushSequence(CmsDer.Constructed0))
            {
                var data = info.ReadSequence(CmsDer.Constructed0).ReadSequence();
                using (writer.PushSequence())
                {
                    CopySignedData(data, writer, token);
                }
            }
        }

        return writer.Encode();
    }

    /// <summary>Copies the SignedData, adding the token to the first signer.</summary>
    /// <param name="data">The SignedData's fields.</param>
    /// <param name="writer">The writer, inside the SignedData sequence.</param>
    /// <param name="token">The token.</param>
    /// <exception cref="AsnContentException">The SignedData is damaged or has no signer.</exception>
    private static void CopySignedData(AsnReader data, AsnWriter writer, ReadOnlySpan<byte> token)
    {
        // The version, the digest algorithms (a SET OF too) and the encapsulated content come first, then any certificates and CRLs.
        for (var field = 0; field < LeadingFields; field++)
        {
            writer.WriteEncodedValue(data.ReadEncodedValue().Span);
        }

        while (data.PeekTag() != Asn1Tag.SetOf)
        {
            writer.WriteEncodedValue(data.ReadEncodedValue().Span);
        }

        var signers = data.ReadSetOf();
        using (writer.PushSetOf())
        {
            CopySigner(signers.ReadSequence(), writer, token);
            while (signers.HasData)
            {
                writer.WriteEncodedValue(signers.ReadEncodedValue().Span);
            }
        }

        while (data.HasData)
        {
            writer.WriteEncodedValue(data.ReadEncodedValue().Span);
        }
    }

    /// <summary>Copies a SignerInfo, adding the token as an unsigned attribute after the signature value and any unsigned attributes.</summary>
    /// <param name="signer">The SignerInfo's fields.</param>
    /// <param name="writer">The writer, inside the signers set.</param>
    /// <param name="token">The token.</param>
    private static void CopySigner(AsnReader signer, AsnWriter writer, ReadOnlySpan<byte> token)
    {
        using (writer.PushSequence())
        {
            AsnReader? existing = null;
            while (signer.HasData)
            {
                if (signer.PeekTag() == PdfMacSignature.UnsignedAttributesTag)
                {
                    existing = signer.ReadSetOf(PdfMacSignature.UnsignedAttributesTag);
                    continue;
                }

                writer.WriteEncodedValue(signer.ReadEncodedValue().Span);
            }

            using (writer.PushSetOf(PdfMacSignature.UnsignedAttributesTag))
            {
                // Unsigned attributes such as a signature timestamp stay; the PDF MAC joins them.
                while (existing is not null && existing.HasData)
                {
                    writer.WriteEncodedValue(existing.ReadEncodedValue().Span);
                }

                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(PdfMacOids.MacDataText);
                    using (writer.PushSetOf())
                    {
                        writer.WriteEncodedValue(token);
                    }
                }
            }
        }
    }
}
