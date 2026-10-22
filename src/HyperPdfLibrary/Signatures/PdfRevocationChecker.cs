// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Looks a certificate up in the CRLs and OCSP responses saved in the document, without the network. A list or response
/// counts only when its signature verifies against the certificate's issuer (or, for OCSP, a responder the issuer
/// authorised). Validity periods of the lists and responses are not checked.
/// </summary>
internal static class PdfRevocationChecker
{
    /// <summary>The context tag of an OCSP response's bytes and a CRL's extensions.</summary>
    private const int ResponseBytesTag = 0;

    /// <summary>The context tag of a good OCSP status.</summary>
    private const int GoodTag = 0;

    /// <summary>The context tag of a revoked OCSP status.</summary>
    private const int RevokedTag = 1;

    /// <summary>The extended key usage of an OCSP responder.</summary>
    private const string OcspSigningOid = "1.3.6.1.5.5.7.3.9";

    /// <summary>Gets the revocation status of a certificate from the saved data.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="issuer">Its issuer, or <see langword="null"/> when the chain did not find one.</param>
    /// <param name="store">The document security store.</param>
    /// <returns>The status.</returns>
    internal static PdfRevocationStatus Check(X509Certificate2 certificate, X509Certificate2? issuer, PdfSecurityStore store)
    {
        if (issuer is null || !store.HasRevocationData)
        {
            return PdfRevocationStatus.Unknown;
        }

        var status = PdfRevocationStatus.Unknown;
        foreach (var crl in store.Crls)
        {
            status = Combine(status, Safely(crl, certificate, issuer, false));
        }

        foreach (var response in store.OcspResponses)
        {
            status = Combine(status, Safely(response, certificate, issuer, true));
        }

        return status;
    }

    /// <summary>Combines two statuses: revoked wins, then not revoked.</summary>
    /// <param name="left">The first status.</param>
    /// <param name="right">The second status.</param>
    /// <returns>The combined status.</returns>
    private static PdfRevocationStatus Combine(PdfRevocationStatus left, PdfRevocationStatus right) => (PdfRevocationStatus)Math.Max((int)left, (int)right);

    /// <summary>Checks one list or response, treating damaged data as unknown.</summary>
    /// <param name="data">The DER data.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="issuer">Its issuer.</param>
    /// <param name="ocsp">Whether the data is an OCSP response rather than a CRL.</param>
    /// <returns>The status.</returns>
    private static PdfRevocationStatus Safely(byte[] data, X509Certificate2 certificate, X509Certificate2 issuer, bool ocsp)
    {
        try
        {
            return ocsp ? CheckOcsp(data, certificate, issuer) : CheckCrl(data, certificate, issuer);
        }
        catch (Exception ex) when (ex is AsnContentException or CryptographicException)
        {
            return PdfRevocationStatus.Unknown;
        }
    }

    /// <summary>Checks a CRL.</summary>
    /// <param name="data">The DER CertificateList.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="issuer">Its issuer.</param>
    /// <returns>The status.</returns>
    private static PdfRevocationStatus CheckCrl(byte[] data, X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var signed = PdfSignedStructure.Read(data);
        if (!signed.IsSignedBy(issuer))
        {
            return PdfRevocationStatus.Unknown;
        }

        var body = new AsnReader(signed.Body, AsnEncodingRules.DER).ReadSequence();
        if (body.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            _ = body.ReadEncodedValue();
        }

        _ = body.ReadEncodedValue();
        if (!body.ReadEncodedValue().Span.SequenceEqual(certificate.IssuerName.RawData))
        {
            return PdfRevocationStatus.Unknown;
        }

        _ = body.ReadEncodedValue();
        if (body.HasData && !body.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence) && body.PeekTag().TagClass == TagClass.Universal)
        {
            _ = body.ReadEncodedValue();
        }

        return body.HasData && body.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence) && ListsSerial(body.ReadSequence(), certificate.SerialNumberBytes.Span)
            ? PdfRevocationStatus.Revoked
            : PdfRevocationStatus.NotRevoked;
    }

    /// <summary>Determines whether a CRL's revoked list holds a serial number.</summary>
    /// <param name="revoked">The revokedCertificates sequence.</param>
    /// <param name="serial">The serial number.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool ListsSerial(AsnReader revoked, ReadOnlySpan<byte> serial)
    {
        while (revoked.HasData)
        {
            if (revoked.ReadSequence().ReadIntegerBytes().Span.SequenceEqual(serial))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks an OCSP response: a full OCSPResponse or a bare BasicOCSPResponse.</summary>
    /// <param name="data">The DER data.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="issuer">Its issuer.</param>
    /// <returns>The status.</returns>
    private static PdfRevocationStatus CheckOcsp(byte[] data, X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var outer = new AsnReader(data, AsnEncodingRules.DER).ReadSequence();
        if (!outer.PeekTag().HasSameClassAndValue(Asn1Tag.Enumerated))
        {
            return CheckBasic(data, certificate, issuer);
        }

        if (outer.ReadEnumeratedValue<OcspStatus>() != OcspStatus.Successful)
        {
            return PdfRevocationStatus.Unknown;
        }

        var bytes = outer.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, ResponseBytesTag)).ReadSequence();
        _ = bytes.ReadObjectIdentifier();
        return CheckBasic(bytes.ReadOctetString(), certificate, issuer);
    }

    /// <summary>Checks a BasicOCSPResponse.</summary>
    /// <param name="data">The DER BasicOCSPResponse.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="issuer">Its issuer.</param>
    /// <returns>The status.</returns>
    private static PdfRevocationStatus CheckBasic(byte[] data, X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var signed = PdfSignedStructure.Read(data);

        // A store holds a response for each certificate of the chain, so most are about another certificate.
        // Matching the certificate is a hash and a compare and a signature check is far dearer, so only a response about this certificate is checked.
        var status = StatusOf(signed.Body, certificate);
        if (status == PdfRevocationStatus.Unknown)
        {
            return status;
        }

        return signed.IsSignedBy(issuer) || IsSignedByResponder(signed, issuer) ? status : PdfRevocationStatus.Unknown;
    }

    /// <summary>Reads the status a response gives a certificate, without checking the response's signature.</summary>
    /// <param name="responseData">The DER ResponseData.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The status the first matching SingleResponse gives; unknown when none is about the certificate.</returns>
    private static PdfRevocationStatus StatusOf(ReadOnlyMemory<byte> responseData, X509Certificate2 certificate)
    {
        var body = new AsnReader(responseData, AsnEncodingRules.DER).ReadSequence();
        if (body.PeekTag().TagClass == TagClass.ContextSpecific && body.PeekTag().TagValue == 0)
        {
            _ = body.ReadEncodedValue();
        }

        _ = body.ReadEncodedValue();
        _ = body.ReadEncodedValue();
        var responses = body.ReadSequence();
        while (responses.HasData)
        {
            var status = CheckSingle(responses.ReadSequence(), certificate);
            if (status != PdfRevocationStatus.Unknown)
            {
                return status;
            }
        }

        return PdfRevocationStatus.Unknown;
    }

    /// <summary>Checks one SingleResponse.</summary>
    /// <param name="single">The SingleResponse contents.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The status when the response is for the certificate; otherwise unknown.</returns>
    private static PdfRevocationStatus CheckSingle(AsnReader single, X509Certificate2 certificate)
    {
        var id = single.ReadSequence();
        var algorithm = id.ReadSequence().ReadObjectIdentifier();
        var nameDigest = id.ReadOctetString();
        _ = id.ReadOctetString();
        var serial = id.ReadIntegerBytes();
        Span<byte> expected = stackalloc byte[PdfAlgorithms.MaxHashLength];
        var length = PdfAlgorithms.Hash(algorithm, certificate.IssuerName.RawData, expected);
        if (length == 0 || !nameDigest.AsSpan().SequenceEqual(expected[..length]) || !serial.Span.SequenceEqual(certificate.SerialNumberBytes.Span))
        {
            return PdfRevocationStatus.Unknown;
        }

        var tag = single.PeekTag();
        return tag.TagClass != TagClass.ContextSpecific
            ? PdfRevocationStatus.Unknown
            : tag.TagValue switch
        {
            GoodTag => PdfRevocationStatus.NotRevoked,
            RevokedTag => PdfRevocationStatus.Revoked,
            _ => PdfRevocationStatus.Unknown,
        };
    }

    /// <summary>Determines whether a response is signed by a responder certificate it carries, which the issuer signed for OCSP signing.</summary>
    /// <param name="signed">The response.</param>
    /// <param name="issuer">The issuer.</param>
    /// <returns><see langword="true"/> when an authorised responder signed the response.</returns>
    private static bool IsSignedByResponder(in PdfSignedStructure signed, X509Certificate2 issuer)
    {
        if (!signed.Rest.HasData)
        {
            return false;
        }

        var certificates = signed.Rest.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, ResponseBytesTag)).ReadSequence();
        while (certificates.HasData)
        {
            var der = certificates.ReadEncodedValue();
            if (!PdfSignedStructure.Read(der).IsSignedBy(issuer))
            {
                continue;
            }

            using var responder = X509CertificateLoader.LoadCertificate(der.Span);
            if (IsOcspSigner(responder) && signed.IsSignedBy(responder))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a certificate may sign OCSP responses.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when its extended key usage includes OCSP signing.</returns>
    private static bool IsOcspSigner(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is not X509EnhancedKeyUsageExtension usage)
            {
                continue;
            }

            foreach (var oid in usage.EnhancedKeyUsages)
            {
                if (oid.Value == OcspSigningOid)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
