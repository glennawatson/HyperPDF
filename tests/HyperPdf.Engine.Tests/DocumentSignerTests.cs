// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks the engine's certificate signer: signed files validate, keep earlier signatures valid, reopen in PDFium and pass qpdf;
/// external signing round-trips; a certified file refuses what it forbids; long-term validation data and document timestamps are added.
/// </summary>
public sealed class DocumentSignerTests
{
    /// <summary>The pages in the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The signatures in a file that holds two.</summary>
    private const int TwoSignatures = 2;

    /// <summary>A reason.</summary>
    private const string Approved = "Approved";

    /// <summary>Where the visible signature goes, in points.</summary>
    private static readonly SigningArea VisibleArea = new(36, 36, 236, 108);

    /// <summary>The signing time every test uses, so files are repeatable.</summary>
    private static readonly DateTimeOffset Time = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The signer used by the tests, made once because key generation is slow.</summary>
    private static readonly X509Certificate2 Signer = PdfSigning.CreateCertificate("Engine Signer", null, false, TimeProvider.System);

    /// <summary>A second signer.</summary>
    private static readonly X509Certificate2 Countersigner = PdfSigning.CreateCertificate("Engine Countersigner", null, false, TimeProvider.System);

    /// <summary>A visible signature validates, covers the file, reopens in both engines and passes qpdf, and the open file is not touched.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedCopyValidatesAndReopensInBothEngines(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);
        var before = await File.ReadAllBytesAsync(source.FilePath, cancellationToken);
        var request = new SigningRequest(1, Approved, "Brisbane", Time) { Area = VisibleArea };
        var outcome = await PdfSigner.SignAsync(source.Document, Signer, request, null, cancellationToken);
        var signed = outcome.Bytes;
        using var reopened = PdfDocumentReader.Open(signed, null);
        using var pdfium = PdfiumReferenceDocument.Open(signed);
        var report = Validate(signed, Signer)[0];
        var qpdf = await QpdfCheck.RunAsync(signed, cancellationToken) ?? string.Empty;

        using (Assert.Multiple())
        {
            await Assert.That(outcome.Notes.Count).IsEqualTo(0);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.CoversWholeDocument).IsTrue();
            await Assert.That(report.Signature.Field.Reason).IsEqualTo(Approved);
            await Assert.That(reopened.PageCount).IsEqualTo(Pages);
            await Assert.That(pdfium.PageCount).IsEqualTo(Pages);
            await Assert.That(qpdf).IsEmpty().Because(qpdf);
            await Assert.That(await File.ReadAllBytesAsync(source.FilePath, cancellationToken)).IsEquivalentTo(before);
        }
    }

    /// <summary>A second signature keeps the first valid, and the file still reopens in PDFium.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondSignatureKeepsTheFirstValid(CancellationToken cancellationToken)
    {
        var first = await SignAsync(TestPdf.Create(Pages), Signer, new(0, "First", string.Empty, Time), cancellationToken);
        var second = await SignAsync(first, Countersigner, new(0, "Second", string.Empty, Time), cancellationToken);
        var reports = Validate(second, Signer, Countersigner);
        using var pdfium = PdfiumReferenceDocument.Open(second);

        using (Assert.Multiple())
        {
            await Assert.That(reports.Count).IsEqualTo(TwoSignatures);
            await Assert.That(reports[0].SignatureValid).IsTrue();
            await Assert.That(reports[0].DigestValid).IsTrue();
            await Assert.That(reports[0].ModifiedAfterSigning).IsTrue();
            await Assert.That(reports[1].SignatureValid).IsTrue();
            await Assert.That(reports[1].CoversWholeDocument).IsTrue();
            await Assert.That(pdfium.PageCount).IsEqualTo(Pages);
        }
    }

    /// <summary>External signing round-trips through the Core interface: the bytes to sign go out, the value comes back and the file validates.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExternalSigningRoundTrips(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);
        var signer = Signing(source.Document);
        var identity = new SignerIdentity([Signer.RawData], SigningKeyKind.RsaPkcs1);
        var pending = await signer.PrepareAsync(identity, new(0, Approved, string.Empty, Time), null, cancellationToken);

        // The key stays with the signer; only the bytes to sign and the value cross over.
        using var key = Signer.GetRSAPrivateKey()!;
        var value = key.SignData(pending.SignedAttributes.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signed = (await signer.CompleteAsync(pending, value, cancellationToken)).Bytes;
        var report = Validate(signed, Signer)[0];

        using (Assert.Multiple())
        {
            await Assert.That(pending.Digest).IsEqualTo(SigningDigest.Sha256);
            await Assert.That(pending.Format).IsEqualTo(SigningFormat.Pkcs7Detached);
            await Assert.That(pending.ContentsCapacity).IsGreaterThan(0);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.DigestValid).IsTrue();
        }
    }

    /// <summary>A signature provider behind the Core interface signs the file.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProviderSignsTheFile(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);
        var provider = new TokenProvider(Signer);
        var signed = (await Signing(source.Document).SignAsync(provider, new(0, Approved, string.Empty, Time), null, cancellationToken)).Bytes;

        using (Assert.Multiple())
        {
            await Assert.That(provider.Calls).IsEqualTo(1);
            await Assert.That(Validate(signed, Signer)[0].SignatureValid).IsTrue();
        }
    }

    /// <summary>A pending signature from another signer is refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForeignPendingSignatureIsRefused(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);

        _ = await Assert.ThrowsAsync<ArgumentException>(async () => await Signing(source.Document).CompleteAsync(new ForeignPending(), new byte[1], cancellationToken));
    }

    /// <summary>A certified file allows what it permits, previews what it forbids, and refuses to be signed against its rules.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertifiedFileRefusesAnotherSignature(CancellationToken cancellationToken)
    {
        var certified = await SignAsync(TestPdf.Create(Pages), Signer, new(0, "Certified", string.Empty, Time) { Certification = CertificationLevel.NoChanges }, cancellationToken);
        using var source = new HyperPdfTestDocument(certified);
        var signer = Signing(source.Document);
        var preview = signer.Preview(new(0, "Again", string.Empty, Time));
        var refused = await Assert.ThrowsAsync<SigningRefusedException>(async () => await signer.SignAsync(Countersigner, new(0, "Again", string.Empty, Time), null, cancellationToken));

        using (Assert.Multiple())
        {
            await Assert.That(preview.CanSign).IsFalse();
            await Assert.That(preview.Problems[0]).Contains("allows no further changes");
            await Assert.That(refused!.Reasons.Count).IsGreaterThan(0);
            await Assert.That(Validate(certified, Signer)[0].Signature.IsCertification).IsTrue();
        }
    }

    /// <summary>A document timestamp is added to a signed file through the Core interface and validates.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentTimestampIsAdded(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(Time);
        var signed = await SignAsync(TestPdf.Create(Pages), Signer, new(0, Approved, string.Empty, Time), cancellationToken);
        using var source = new HyperPdfTestDocument(signed);
        var stamped = (await Signing(source.Document).AddDocumentTimestampAsync(authority, SigningDigest.Sha256, cancellationToken)).Bytes;
        var reports = Validate(stamped, Signer, authority.Certificate);

        using (Assert.Multiple())
        {
            await Assert.That(reports.Count).IsEqualTo(TwoSignatures);
            await Assert.That(reports[0].SignatureValid).IsTrue();
            await Assert.That(reports[1].Signature.IsDocumentTimestamp).IsTrue();
            await Assert.That(reports[1].TimestampValid).IsTrue();
        }
    }

    /// <summary>Long-term validation data is collected from a source and written to the security store with an entry for the signature.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationDataIsAddedToTheSecurityStore(CancellationToken cancellationToken)
    {
        using var authority = PdfSigning.CreateCertificate("Engine Root", null, true, TimeProvider.System);
        using var issued = PdfSigning.CreateCertificate("Engine Issued", authority, false, TimeProvider.System);
        var signed = await SignAsync(TestPdf.Create(Pages), issued, new(0, Approved, string.Empty, Time), cancellationToken);
        using var source = new HyperPdfTestDocument(signed);
        var revocation = new IssuerSource(authority);
        var updated = (await Signing(source.Document).AddValidationDataAsync(revocation, true, cancellationToken)).Bytes;
        using var reopened = PdfDocumentReader.Open(updated, null);
        var store = PdfDocumentSignatureValidation.GetSecurityStore(reopened);
        var signature = PdfDocumentAttachments.GetSignatures(reopened)[0];
        var report = Validate(updated, authority)[0];
        using var pdfium = PdfiumReferenceDocument.Open(updated);

        using (Assert.Multiple())
        {
            await Assert.That(revocation.CrlRequests).IsGreaterThan(0);
            await Assert.That(store.Crls.Length).IsEqualTo(1);
            await Assert.That(store.FindVri(signature.Contents, signature.Contents.Length)).IsNotNull();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.PermittedByMdp).IsTrue();
            await Assert.That(pdfium.PageCount).IsEqualTo(Pages);
        }
    }

    /// <summary>A document without a signer feature reports that it cannot sign.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentCanBeSigned()
    {
        using var source = new HyperPdfTestDocument(Pages);

        await Assert.That(PdfSigner.CanSign(source.Document)).IsTrue();
        await Assert.That(PdfSigner.CanSign(null)).IsFalse();
    }

    /// <summary>Gets the signer feature of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The signer.</returns>
    private static IDocumentSigner Signing(IDocument document) => DocumentFeature<IDocumentSigner>.Get(document)!;

    /// <summary>Signs a file through the engine.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="cancellationToken">Cancels the signing.</param>
    /// <returns>The signed file.</returns>
    private static async Task<byte[]> SignAsync(byte[] file, X509Certificate2 certificate, SigningRequest request, CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(file);
        return (await PdfSigner.SignAsync(source.Document, certificate, request, null, cancellationToken)).Bytes;
    }

    /// <summary>Validates every signature of a file, trusting some certificates.</summary>
    /// <param name="file">The file.</param>
    /// <param name="roots">The trusted roots.</param>
    /// <returns>The reports.</returns>
    private static IReadOnlyList<PdfSignatureValidationReport> Validate(byte[] file, params X509Certificate2[] roots)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [.. roots] });
    }

    /// <summary>A signer whose key stays out of the app, as a token's does.</summary>
    private sealed class TokenProvider : ISignatureProvider
    {
        /// <summary>The certificate holding the private key.</summary>
        private readonly X509Certificate2 _certificate;

        /// <summary>Initializes a new instance of the <see cref="TokenProvider"/> class.</summary>
        /// <param name="certificate">The certificate holding the private key.</param>
        internal TokenProvider(X509Certificate2 certificate) => _certificate = certificate;

        /// <inheritdoc/>
        public SignerIdentity Identity => new([_certificate.RawData], SigningKeyKind.RsaPkcs1);

        /// <summary>Gets the times the token was asked to sign.</summary>
        internal int Calls { get; private set; }

        /// <inheritdoc/>
        public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, SigningDigest digest, CancellationToken cancellationToken)
        {
            Calls++;
            using var key = _certificate.GetRSAPrivateKey()!;
            return ValueTask.FromResult(key.SignData(data.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }

    /// <summary>A pending signature the engine did not prepare.</summary>
    private sealed class ForeignPending : IPendingSignature
    {
        /// <inheritdoc/>
        public ReadOnlyMemory<byte> SignedAttributes => default;

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> DocumentDigest => default;

        /// <inheritdoc/>
        public SigningDigest Digest => SigningDigest.Sha256;

        /// <inheritdoc/>
        public SigningFormat Format => SigningFormat.Pkcs7Detached;

        /// <inheritdoc/>
        public string FieldName => string.Empty;

        /// <inheritdoc/>
        public int ContentsCapacity => 0;
    }

    /// <summary>Finds an issuer and an empty revocation list the way an online source would.</summary>
    private sealed class IssuerSource : ISignatureRevocationSource
    {
        /// <summary>The issuer, which also signs the list.</summary>
        private readonly X509Certificate2 _authority;

        /// <summary>Initializes a new instance of the <see cref="IssuerSource"/> class.</summary>
        /// <param name="authority">The issuer.</param>
        internal IssuerSource(X509Certificate2 authority) => _authority = authority;

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
