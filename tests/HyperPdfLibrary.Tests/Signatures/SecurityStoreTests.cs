// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests for the document security store, /VRI entries and revocation from saved CRLs.</summary>
public sealed class SecurityStoreTests
{
    /// <summary>Gets a stand-in OCSP response; the store lists it as data.</summary>
    private static ReadOnlySpan<byte> OcspStandIn => [0x30, 0x03, 0x0A, 0x01, 0x06];

    /// <summary>The empty store returned for a document does not expose a writable shared /VRI map.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyStoreVriIsReadOnly()
    {
        using var document = PdfDocumentReader.Open(SignatureSamples.Unsigned(), null);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(document);
        var writable = store.Vri is IDictionary<string, PdfValidationRelatedInfo> dictionary && !dictionary.IsReadOnly;

        await Assert.That(store.Vri.Count).IsEqualTo(0);
        await Assert.That(writable).IsFalse();
    }

    /// <summary>The store's certificates, OCSP responses and CRLs are listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StoreIsListed()
    {
        using var authority = PdfSigning.CreateCertificate("Store Root", null, true, TimeProvider.System);
        var crl = BuildCrl(authority, null);
        var file = SignatureSamples.AddSecurityStore(Sign(), SignatureSamples.SignedCatalog(0), [authority.RawData, OcspStandIn.ToArray(), crl], new(1, 1), string.Empty);
        using var document = PdfDocumentReader.Open(file, null);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(document);

        await Assert.That(store.Certificates.Length).IsEqualTo(1);
        await Assert.That(store.Certificates[0]).IsEquivalentTo(authority.RawData);
        await Assert.That(store.OcspResponses[0]).IsEquivalentTo(OcspStandIn.ToArray());
        await Assert.That(store.Crls[0]).IsEquivalentTo(crl);
        await Assert.That(store.HasRevocationData).IsTrue();
    }

    /// <summary>A /VRI entry keyed by the SHA-1 of the signature's /Contents is found for that signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VriEntryIsFound()
    {
        var signed = Sign();
        string key;
        using (var original = PdfDocumentReader.Open(signed, null))
        {
            key = Convert.ToHexString(CryptographicOperations.HashData(HashAlgorithmName.SHA1, PdfDocumentAttachments.GetSignatures(original)[0].Contents));
        }

        var vri = $"<< /{key} << /Cert [12 0 R] /TU (D:20260102030405Z) >> >>";
        var file = SignatureSamples.AddSecurityStore(signed, SignatureSamples.SignedCatalog(0), [SignatureFixtures.Signer.RawData], new(1, 0), vri);
        using var document = PdfDocumentReader.Open(file, null);
        var signature = PdfDocumentAttachments.GetSignatures(document)[0];
        var entry = PdfDocumentSignatureValidation.GetSecurityStore(document).FindVri(signature.Contents, signature.Contents.Length);

        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Certificates.Length).IsEqualTo(1);
        await Assert.That(entry.CreatedAt).IsEqualTo(PdfSigning.SigningTime);
    }

    /// <summary>A saved CRL from the issuer that lists the signer marks it revoked; one that does not marks it not revoked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CrlGivesRevocationStatus()
    {
        using var authority = PdfSigning.CreateCertificate("Issuing Root", null, true, TimeProvider.System);
        using var signer = PdfSigning.CreateCertificate("Issued Signer", authority, false, TimeProvider.System);
        var signed = SignatureSamples.Signed(signer, 0, string.Empty);

        await Assert.That(StatusWith(signed, authority, BuildCrl(authority, signer))).IsEqualTo(PdfRevocationStatus.Revoked);
        await Assert.That(StatusWith(signed, authority, BuildCrl(authority, null))).IsEqualTo(PdfRevocationStatus.NotRevoked);
    }

    /// <summary>A CRL signed by someone else is ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForeignCrlIsIgnored()
    {
        using var authority = PdfSigning.CreateCertificate("Real Root", null, true, TimeProvider.System);
        using var stranger = PdfSigning.CreateCertificate("Real Root", null, true, TimeProvider.System);
        using var signer = PdfSigning.CreateCertificate("Issued Signer", authority, false, TimeProvider.System);
        var signed = SignatureSamples.Signed(signer, 0, string.Empty);

        await Assert.That(StatusWith(signed, authority, BuildCrl(stranger, signer))).IsEqualTo(PdfRevocationStatus.Unknown);
    }

    /// <summary>Signs the sample form with the shared signer.</summary>
    /// <returns>The signed file.</returns>
    private static byte[] Sign() => SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty);

    /// <summary>Adds a store with the issuer's certificate and a CRL, then reads the signer's revocation status.</summary>
    /// <param name="signed">The signed file.</param>
    /// <param name="authority">The issuer, trusted as the root.</param>
    /// <param name="crl">The CRL.</param>
    /// <returns>The status.</returns>
    private static PdfRevocationStatus StatusWith(byte[] signed, X509Certificate2 authority, byte[] crl)
    {
        var file = SignatureSamples.AddSecurityStore(signed, SignatureSamples.SignedCatalog(0), [authority.RawData, crl], new(1, 0), string.Empty);
        var report = SignatureFixtures.Validate(file, authority)[0];
        return report.Chain!.IsTrusted ? report.Chain.Revocation : PdfRevocationStatus.Unknown;
    }

    /// <summary>Builds a CRL.</summary>
    /// <param name="issuer">The issuer, with its private key.</param>
    /// <param name="revoked">A certificate to list as revoked, or <see langword="null"/>.</param>
    /// <returns>The DER CRL.</returns>
    private static byte[] BuildCrl(X509Certificate2 issuer, X509Certificate2? revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
        {
            builder.AddEntry(revoked);
        }

        return builder.Build(issuer, BigInteger.One, issuer.NotAfter, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
