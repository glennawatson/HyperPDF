// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Writes a CMS ContentInfo holding EnvelopedData (RFC 5652 6) for a public-key encrypted PDF (ISO 32000-2 7.6.5.3): one
/// random AES-256 content key encrypts the content in CBC mode, and each recipient gets that key by RSA key transport or
/// ECDH key agreement. The reading side is <see cref="CmsRecipientOpening"/>.
/// </summary>
internal static class CmsEnvelopedDataWriter
{
    /// <summary>The version of EnvelopedData when every recipient is a version 0 key transport recipient.</summary>
    private const int PlainVersion = 0;

    /// <summary>The version of EnvelopedData when a recipient is not version 0, such as a key agreement recipient (RFC 5652 6.1).</summary>
    private const int ExtendedVersion = 2;

    /// <summary>Encrypts content for a list of recipients.</summary>
    /// <param name="content">The content: the 20-byte seed and the 4 bytes of permissions.</param>
    /// <param name="recipients">The recipients' certificates.</param>
    /// <param name="fill">The random source for the content key and the initialisation vector.</param>
    /// <returns>The DER of the ContentInfo.</returns>
    /// <exception cref="PdfException">A certificate cannot receive an encrypted file.</exception>
    internal static byte[] Write(ReadOnlySpan<byte> content, IReadOnlyList<X509Certificate2> recipients, FillBytes fill)
    {
        Span<byte> contentKey = stackalloc byte[CmsAlgorithms.Aes256KeyLength];
        Span<byte> iv = stackalloc byte[PdfSecurityHandler.AesBlock];
        fill(contentKey);
        fill(iv);
        try
        {
            byte[] encrypted;
            using (var aes = Aes.Create())
            {
                aes.SetKey(contentKey);
                encrypted = aes.EncryptCbc(content, iv);
            }

            return Build(recipients, contentKey, iv, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    /// <summary>Writes the ContentInfo around the recipients and the encrypted content.</summary>
    /// <param name="recipients">The recipients' certificates.</param>
    /// <param name="contentKey">The content-encryption key.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="encrypted">The encrypted content.</param>
    /// <returns>The DER.</returns>
    private static byte[] Build(IReadOnlyList<X509Certificate2> recipients, ReadOnlySpan<byte> contentKey, ReadOnlySpan<byte> iv, byte[] encrypted)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(CmsWriteOids.EnvelopedData);
            using (writer.PushSequence(CmsDer.Constructed0))
            using (writer.PushSequence())
            {
                writer.WriteInteger(WriteRecipients(recipients, contentKey, out var recipientInfos));
                writer.WriteEncodedValue(recipientInfos);
                WriteEncryptedContent(writer, iv, encrypted);
            }
        }

        return writer.Encode();
    }

    /// <summary>Writes the set of RecipientInfos, which DER sorts.</summary>
    /// <param name="recipients">The recipients' certificates.</param>
    /// <param name="contentKey">The content-encryption key.</param>
    /// <param name="recipientInfos">Receives the DER of the set.</param>
    /// <returns>The EnvelopedData version.</returns>
    /// <exception cref="PdfException">A certificate cannot receive an encrypted file.</exception>
    private static int WriteRecipients(IReadOnlyList<X509Certificate2> recipients, ReadOnlySpan<byte> contentKey, out byte[] recipientInfos)
    {
        var version = PlainVersion;
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        {
            foreach (var certificate in recipients)
            {
                switch (CmsRecipientKinds.Classify(certificate))
                {
                    case CmsRecipientKind.Rsa:
                        {
                            CmsKeyTransportWriter.Write(writer, certificate, contentKey);
                            break;
                        }

                    case CmsRecipientKind.EllipticCurve:
                        {
                            CmsKeyAgreementWriter.Write(writer, certificate, contentKey);
                            version = ExtendedVersion;
                            break;
                        }

                    default:
                        {
                            throw new PdfException(
                                PdfError.Certificate,
                                $"The certificate for {certificate.Subject} cannot be used to encrypt a file. "
                                + "It needs an RSA key that allows key encipherment, or an elliptic curve key that allows key agreement.");
                        }
                }
            }
        }

        recipientInfos = writer.Encode();
        return version;
    }

    /// <summary>Writes the EncryptedContentInfo: content type data, AES-256-CBC with the IV as its parameter, and the encrypted content as <c>[0] IMPLICIT</c>.</summary>
    /// <param name="writer">The DER writer.</param>
    /// <param name="iv">The initialisation vector.</param>
    /// <param name="encrypted">The encrypted content.</param>
    private static void WriteEncryptedContent(AsnWriter writer, ReadOnlySpan<byte> iv, byte[] encrypted)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(CmsWriteOids.Data);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(CmsWriteOids.Aes256Cbc);
                writer.WriteOctetString(iv);
            }

            writer.WriteOctetString(encrypted, CmsDer.Primitive0);
        }
    }
}
