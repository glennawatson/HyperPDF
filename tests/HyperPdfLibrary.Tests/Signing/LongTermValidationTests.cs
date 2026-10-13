// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// Tests for signature timestamps (RFC 3161), document timestamps (ISO 32000-2, 12.8.5) and the long-term validation data
/// the document security store holds (12.8.4.3).
/// </summary>
public sealed class LongTermValidationTests
{
    /// <summary>The signatures in a file with one signature and one document timestamp.</summary>
    private const int SignatureAndTimestamp = 2;

    /// <summary>A signature timestamp is requested from the authority and validates, and the signature stays valid.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureTimestampValidates(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var signed = await SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Timestamp = new TimestamperClient(authority) });
        var report = SignatureFixtures.Validate(signed.Bytes, SignatureFixtures.Signer)[0];

        using (Assert.Multiple())
        {
            await Assert.That(authority.Issued).IsEqualTo(1);
            await Assert.That(report.HasTimestamp).IsTrue();
            await Assert.That(report.TimestampValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Timestamp!.Time).IsEqualTo(SigningFixtures.Time);
        }
    }

    /// <summary>The room reserved grows with the authority's estimate of its token, so a stamped signature still fits at once.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TimestampedSignatureFitsInOneAttempt(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var signed = await SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Timestamp = new TimestamperClient(authority) });

        await Assert.That(signed.Attempts).IsEqualTo(1);
    }

    /// <summary>A document timestamp covers a signed file, validates, and leaves the signature valid.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentTimestampCoversASignedFile(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new());
        using var document = PdfDocumentReader.Open(signed, null);
        var stamped = await PdfDocumentSigning.AddDocumentTimestampAsync(document, new() { Timestamp = new TimestamperClient(authority) }, cancellationToken);
        var reports = SigningFixtures.Validate(stamped.Bytes, SignatureFixtures.Signer, authority.Certificate);

        using (Assert.Multiple())
        {
            await Assert.That(stamped.FieldName).IsEqualTo(reports[1].Signature.FieldName);
            await Assert.That(reports.Count).IsEqualTo(SignatureAndTimestamp);
            await Assert.That(reports[0].SignatureValid).IsTrue();
            await Assert.That(reports[1].Signature.IsDocumentTimestamp).IsTrue();
            await Assert.That(reports[1].TimestampValid).IsTrue();
            await Assert.That(reports[1].CoversWholeDocument).IsTrue();
        }
    }

    /// <summary>A document timestamp needs a timestamp client.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentTimestampNeedsAClient(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);

        _ = await Assert.ThrowsAsync<ArgumentException>(async () => await PdfDocumentSigning.AddDocumentTimestampAsync(document, new(), cancellationToken));
    }

    /// <summary>Validation data is collected for a signature, written to the security store with a /VRI entry, and the file still validates with the data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationDataIsWrittenToTheSecurityStore(CancellationToken cancellationToken)
    {
        using var authority = PdfSigning.CreateCertificate("Validation Root", null, true, TimeProvider.System);
        using var signer = PdfSigning.CreateCertificate("Validated Signer", authority, false, TimeProvider.System);
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), signer, new());
        var source = new FakeRevocationSource(authority);
        using var document = PdfDocumentReader.Open(signed, null);
        var collected = await PdfDocumentSigning.CollectValidationDataAsync(document, source, cancellationToken);
        var updated = await PdfDocumentSigning.AddValidationDataAsync(document, collected, true, cancellationToken);
        using var reopened = PdfDocumentReader.Open(updated.Bytes, null);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(reopened);
        var signature = PdfDocumentAttachments.GetSignatures(reopened)[0];
        var report = SignatureFixtures.Validate(updated.Bytes, authority)[0];

        using (Assert.Multiple())
        {
            await Assert.That(collected.Count).IsEqualTo(1);
            await Assert.That(source.CrlRequests).IsGreaterThan(0);
            await Assert.That(Array.Exists(store.Certificates, item => item.AsSpan().SequenceEqual(authority.RawData))).IsTrue();
            await Assert.That(store.Crls.Length).IsEqualTo(1);
            await Assert.That(store.FindVri(signature.Contents, signature.Contents.Length)).IsNotNull();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.SecurityStore);
            await Assert.That(report.Chain!.IsTrusted).IsTrue();
            await Assert.That(report.Chain.Revocation).IsEqualTo(PdfRevocationStatus.NotRevoked);
        }
    }

    /// <summary>Data added to a store that already has data keeps what was there, and a file certified for no changes still allows the store.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondUpdateKeepsEarlierData(CancellationToken cancellationToken)
    {
        var certified = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.NoChanges });
        using var first = PdfDocumentReader.Open(certified, null);
        var one = await PdfDocumentSigning.AddValidationDataAsync(first, [new([], [SignatureFixtures.Signer.RawData], [], [])], false, cancellationToken);
        using var second = PdfDocumentReader.Open(one.Bytes, null);
        var two = await PdfDocumentSigning.AddValidationDataAsync(second, [new([], [SigningFixtures.SecondSigner.RawData], [], [])], false, cancellationToken);
        using var reopened = PdfDocumentReader.Open(two.Bytes, null);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(reopened);
        var report = SignatureFixtures.ValidateSingle(two.Bytes);

        using (Assert.Multiple())
        {
            await Assert.That(store.Certificates.Length).IsEqualTo(SignatureAndTimestamp);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.PermittedByMdp).IsTrue();
        }
    }

    /// <summary>Finds an issuer and a list of revoked certificates the way an online source would.</summary>
    private sealed class FakeRevocationSource : IPdfRevocationSource
    {
        /// <summary>The issuer, which also signs the list.</summary>
        private readonly X509Certificate2 _authority;

        /// <summary>Initializes a new instance of the <see cref="FakeRevocationSource"/> class.</summary>
        /// <param name="authority">The issuer.</param>
        internal FakeRevocationSource(X509Certificate2 authority) => _authority = authority;

        /// <summary>Gets the times a list was asked for.</summary>
        internal int CrlRequests { get; private set; }

        /// <inheritdoc/>
        public ValueTask<byte[]?> GetOcspResponseAsync(byte[] certificate, byte[] issuer, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

        /// <inheritdoc/>
        public ValueTask<byte[]?> GetCrlAsync(byte[] certificate, CancellationToken cancellationToken)
        {
            CrlRequests++;
            var builder = new CertificateRevocationListBuilder();
            return ValueTask.FromResult<byte[]?>(builder.Build(_authority, BigInteger.One, _authority.NotAfter, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        /// <inheritdoc/>
        public ValueTask<byte[]?> GetIssuerAsync(byte[] certificate, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(_authority.RawData);
    }
}
