// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>Matches CMS recipient identifiers against a certificate.</summary>
internal static class CmsRecipientMatching
{
    /// <summary>Reads the issuer and serial number a recipient can name a certificate by.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The identity.</returns>
    internal static CmsRecipientIdentity Identify(X509Certificate2 certificate) =>
        new(certificate, certificate.IssuerName.RawData, certificate.SerialNumberBytes);

    /// <summary>
    /// Determines whether an IssuerAndSerialNumber names the certificate: the issuer's DER and the serial number's bytes
    /// are the same.
    /// </summary>
    /// <param name="contents">The IssuerAndSerialNumber's contents.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    internal static bool IssuerAndSerialMatches(ReadOnlySpan<byte> contents, CmsRecipientIdentity identity)
    {
        var issuer = CmsDer.ReadEncoded(ref contents);
        var serial = CmsDer.ReadContents(ref contents, Asn1Tag.Integer);
        return issuer.SequenceEqual(identity.Issuer.Span) && serial.SequenceEqual(identity.SerialNumber.Span);
    }

    /// <summary>
    /// Determines whether a subject key identifier names the certificate. The certificate's identifier is read only here,
    /// because most recipients name the issuer and serial number instead.
    /// </summary>
    /// <param name="identifier">The identifier's bytes.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    internal static bool SubjectKeyMatches(ReadOnlySpan<byte> identifier, CmsRecipientIdentity identity) =>
        !identifier.IsEmpty && identifier.SequenceEqual(SubjectKeyIdentifier(identity.Certificate).Span);

    /// <summary>
    /// Gets the certificate's subject key identifier: its extension's value, or the SHA-1 of the public key bits that
    /// RFC 5280 4.2.1.2 describes when there is no extension.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The identifier.</returns>
    private static ReadOnlyMemory<byte> SubjectKeyIdentifier(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509SubjectKeyIdentifierExtension subjectKey)
            {
                return subjectKey.SubjectKeyIdentifierBytes;
            }
        }

        return CryptographicOperations.HashData(HashAlgorithmName.SHA1, certificate.PublicKey.EncodedKeyValue.RawData);
    }
}
