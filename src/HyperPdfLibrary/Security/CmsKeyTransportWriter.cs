// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Writes CMS key transport recipients (RFC 5652 6.2.1) for RSA certificates: the content-encryption key encrypted with the
/// recipient's public key using RSAES-PKCS1-v1_5, the <c>rsaEncryption</c> algorithm that PDF readers use (RFC 3370 4.2.1).
/// </summary>
internal static class CmsKeyTransportWriter
{
    /// <summary>The version of a KeyTransRecipientInfo that names the recipient by issuer and serial number.</summary>
    private const int IssuerAndSerialVersion = 0;

    /// <summary>Writes a KeyTransRecipientInfo.</summary>
    /// <param name="writer">The DER writer, inside the RecipientInfos set.</param>
    /// <param name="certificate">The recipient's certificate.</param>
    /// <param name="contentKey">The content-encryption key.</param>
    /// <exception cref="PdfException">The certificate has no usable RSA public key.</exception>
    internal static void Write(AsnWriter writer, X509Certificate2 certificate, ReadOnlySpan<byte> contentKey)
    {
        using var rsa = certificate.GetRSAPublicKey() ?? throw new PdfException(PdfError.Certificate, $"The certificate for {certificate.Subject} has no RSA public key.");
        var encrypted = rsa.Encrypt(contentKey, RSAEncryptionPadding.Pkcs1);
        using (writer.PushSequence())
        {
            writer.WriteInteger(IssuerAndSerialVersion);
            CmsRecipientWriting.WriteIssuerAndSerial(writer, certificate);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(CmsWriteOids.RsaEncryption);
                writer.WriteNull();
            }

            writer.WriteOctetString(encrypted);
        }
    }
}
