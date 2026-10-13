// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Opens CMS key transport recipients (RFC 5652 6.2.1), whose content key is encrypted with the recipient's RSA public
/// key. <see cref="EnvelopedCms"/> decrypts them on every platform.
/// </summary>
internal static class CmsKeyTransport
{
    /// <summary>Opens a KeyTransRecipientInfo when its identifier names the certificate.</summary>
    /// <param name="envelope">The whole CMS ContentInfo, which <see cref="EnvelopedCms"/> decodes.</param>
    /// <param name="recipientInfo">The KeyTransRecipientInfo's contents.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="AsnContentException">The recipient identifier is damaged.</exception>
    internal static CmsRecipientResult Open(byte[] envelope, ReadOnlySpan<byte> recipientInfo, CmsRecipientIdentity identity)
    {
        _ = CmsDer.ReadContents(ref recipientInfo, Asn1Tag.Integer);
        if (!IdentifierMatches(recipientInfo, identity))
        {
            return CmsRecipientResult.NotForCertificate;
        }

        var certificate = identity.Certificate;
        if (!certificate.HasPrivateKey)
        {
            return CmsRecipientResult.PrivateKeyMissing;
        }

        using var key = PrivateKey(certificate);
        return key is null ? CmsRecipientResult.Unsupported : Decrypt(envelope, certificate, key);
    }

    /// <summary>Reads a SignerIdentifier: an IssuerAndSerialNumber, or a <c>[0] IMPLICIT</c> subject key identifier.</summary>
    /// <param name="source">What follows the version.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns><see langword="true"/> when the identifier names the certificate.</returns>
    /// <exception cref="AsnContentException">The identifier is damaged.</exception>
    private static bool IdentifierMatches(ReadOnlySpan<byte> source, CmsRecipientIdentity identity) =>
        CmsDer.StartsWith(source, CmsDer.Primitive0)
            ? CmsRecipientMatching.SubjectKeyMatches(CmsDer.ReadContents(ref source, CmsDer.Primitive0), identity)
            : CmsRecipientMatching.IssuerAndSerialMatches(CmsDer.ReadContents(ref source, Asn1Tag.Sequence), identity);

    /// <summary>Decrypts the envelope with the RSA key.</summary>
    /// <param name="envelope">The CMS ContentInfo.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="key">The certificate's private key.</param>
    /// <returns>The content, or a damaged result when the key or content does not decrypt.</returns>
    private static CmsRecipientResult Decrypt(byte[] envelope, X509Certificate2 certificate, RSA key)
    {
        try
        {
            var cms = new EnvelopedCms();
            cms.Decode(envelope);
            foreach (var info in cms.RecipientInfos)
            {
                if (info.Type != RecipientInfoType.KeyTransport || !info.RecipientIdentifier.MatchesCertificate(certificate))
                {
                    continue;
                }

                cms.Decrypt(info, key);
                return CmsRecipientResult.Opened(cms.ContentInfo.Content);
            }
        }
        catch (CryptographicException)
        {
            // The recipient names this certificate, so a key or content that does not decrypt is reported as damaged.
        }

        return CmsRecipientResult.Damaged;
    }

    /// <summary>Gets the certificate's RSA private key.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The key, or <see langword="null"/> when the certificate's key is not an RSA key.</returns>
    private static RSA? PrivateKey(X509Certificate2 certificate)
    {
        try
        {
            return certificate.GetRSAPrivateKey();
        }
        catch (CryptographicException)
        {
            // The provider holds the key but cannot use it for RSA decryption.
            return null;
        }
    }
}
