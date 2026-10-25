// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.TestAssets;

/// <summary>Builds signed basic OCSP responses (RFC 6960) for test certificates.</summary>
public static class OcspResponses
{
    /// <summary>The OID of SHA-256, which names the hash of the issuer in a response.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The OID of ECDSA with SHA-256.</summary>
    private const string EcdsaSha256Oid = "1.2.840.10045.4.3.2";

    /// <summary>The tag of a responder name in a response.</summary>
    private const int ResponderNameTag = 1;

    /// <summary>The tag of the status that says a certificate is good.</summary>
    private const int GoodTag = 0;

    /// <summary>The tag of the status that says a certificate is revoked.</summary>
    private const int RevokedTag = 1;

    /// <summary>Builds a signed basic OCSP response that gives one certificate a status.</summary>
    /// <param name="issuerKey">The issuer's ECDSA key, which signs the response.</param>
    /// <param name="issuer">The issuing certificate.</param>
    /// <param name="subject">The certificate the response is about.</param>
    /// <param name="at">When the response was produced.</param>
    /// <param name="revoked">Whether the response says the certificate is revoked, rather than good.</param>
    /// <returns>The DER response.</returns>
    public static byte[] Build(ECDsa issuerKey, X509Certificate2 issuer, X509Certificate2 subject, DateTimeOffset at, bool revoked)
    {
        ArgumentNullException.ThrowIfNull(issuerKey);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(subject);
        var body = ResponseData(issuer, subject, at, revoked);
        var response = new AsnWriter(AsnEncodingRules.DER);
        using (response.PushSequence())
        {
            response.WriteEncodedValue(body);
            using (response.PushSequence())
            {
                response.WriteObjectIdentifier(EcdsaSha256Oid);
            }

            response.WriteBitString(issuerKey.SignData(body, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        }

        return response.Encode();
    }

    /// <summary>Builds the signed part of a response: the responder, the time and one single response.</summary>
    /// <param name="issuer">The issuing certificate.</param>
    /// <param name="subject">The certificate the response is about.</param>
    /// <param name="at">When the response was produced.</param>
    /// <param name="revoked">Whether the certificate is revoked.</param>
    /// <returns>The DER ResponseData.</returns>
    private static byte[] ResponseData(X509Certificate2 issuer, X509Certificate2 subject, DateTimeOffset at, bool revoked)
    {
        var data = new AsnWriter(AsnEncodingRules.DER);
        using (data.PushSequence())
        {
            using (data.PushSequence(new Asn1Tag(TagClass.ContextSpecific, ResponderNameTag, true)))
            {
                data.WriteEncodedValue(issuer.SubjectName.RawData);
            }

            data.WriteGeneralizedTime(at);
            using (data.PushSequence())
            {
                data.WriteEncodedValue(SingleResponse(issuer, subject, at, revoked));
            }
        }

        return data.Encode();
    }

    /// <summary>Builds the single response for a certificate.</summary>
    /// <param name="issuer">The issuing certificate.</param>
    /// <param name="subject">The certificate the response is about.</param>
    /// <param name="at">When the response was produced.</param>
    /// <param name="revoked">Whether the certificate is revoked.</param>
    /// <returns>The DER SingleResponse.</returns>
    private static byte[] SingleResponse(X509Certificate2 issuer, X509Certificate2 subject, DateTimeOffset at, bool revoked)
    {
        var single = new AsnWriter(AsnEncodingRules.DER);
        using (single.PushSequence())
        {
            using (single.PushSequence())
            {
                using (single.PushSequence())
                {
                    single.WriteObjectIdentifier(Sha256Oid);
                    single.WriteNull();
                }

                single.WriteOctetString(SHA256.HashData(subject.IssuerName.RawData));
                single.WriteOctetString(SHA256.HashData(issuer.PublicKey.EncodedKeyValue.RawData));
                single.WriteInteger(subject.SerialNumberBytes.Span);
            }

            if (revoked)
            {
                using (single.PushSequence(new Asn1Tag(TagClass.ContextSpecific, RevokedTag, true)))
                {
                    single.WriteGeneralizedTime(at);
                }
            }
            else
            {
                single.WriteNull(new Asn1Tag(TagClass.ContextSpecific, GoodTag));
            }

            single.WriteGeneralizedTime(at);
        }

        return single.Encode();
    }
}
