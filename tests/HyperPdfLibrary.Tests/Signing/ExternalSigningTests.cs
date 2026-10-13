// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// Tests for external signing (ISO 32000-2, 12.8.3): the library returns the bytes to sign, a signer apart from the library
/// signs them, and the signature value or container is accepted later.
/// </summary>
public sealed class ExternalSigningTests
{
    /// <summary>A reason.</summary>
    private const string Approved = "Approved";

    /// <summary>The bytes in a SHA-256 digest.</summary>
    private const int Sha256Length = 32;

    /// <summary>The size of a signature value that cannot fit in the room reserved.</summary>
    private const int OversizedSignature = 64_000;

    /// <summary>The hash algorithm of the default digest.</summary>
    private static readonly HashAlgorithmName DefaultHash = HashAlgorithmName.SHA256;

    /// <summary>A signature value made outside the library is accepted and the file validates.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureValueRoundTripValidates(CancellationToken cancellationToken)
    {
        var certificate = SignatureFixtures.Signer;
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, Options(), Identity(certificate), cancellationToken);

        // The signer holds the key; the library never sees it.
        using var key = certificate.GetRSAPrivateKey()!;
        var value = key.SignData(pending.SignedAttributes, DefaultHash, RSASignaturePadding.Pkcs1);
        var signed = await PdfDocumentSigning.CompleteAsync(pending, value, cancellationToken);
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);

        using (Assert.Multiple())
        {
            await Assert.That(pending.Digest).IsEqualTo(PdfDigestMethod.Sha256);
            await Assert.That(pending.DocumentDigest.Length).IsEqualTo(Sha256Length);
            await Assert.That(pending.FieldName).IsEqualTo(signed.FieldName);
            await Assert.That(signed.ContentsUsed).IsLessThanOrEqualTo(pending.ContentsCapacity);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.CoversWholeDocument).IsTrue();
        }
    }

    /// <summary>A container built by a signing service over the document digest is accepted and the file validates.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContainerRoundTripValidates(CancellationToken cancellationToken)
    {
        var certificate = SignatureFixtures.Signer;
        var source = SignatureFixtures.Blank();
        var first = await PrepareAndCompleteAsync(source, certificate, cancellationToken);

        // A service gets the bytes the signature covers and returns a whole container; the signing time makes the file repeatable.
        var covered = SigningFixtures.SignedBytes(first.Bytes, out _);
        var cms = new SignedCms(new ContentInfo(covered), true);
        var signer = new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly };
        cms.ComputeSignature(signer);
        using var document = PdfDocumentReader.Open(source, null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, Options(), Identity(certificate), cancellationToken);
        var signed = await PdfDocumentSigning.CompleteWithContainerAsync(pending, cms.Encode(), cancellationToken);
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);

        using (Assert.Multiple())
        {
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.CoversWholeDocument).IsTrue();
        }
    }

    /// <summary>A signature provider that signs apart from the library is asked for the signature value and the file validates.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProviderSignsAndFileValidates(CancellationToken cancellationToken)
    {
        var provider = new RemoteSigner(SignatureFixtures.Signer);
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var signed = await PdfDocumentSigning.SignAsync(document, Options(), provider, cancellationToken);
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);

        using (Assert.Multiple())
        {
            await Assert.That(provider.Calls).IsEqualTo(1);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.DigestValid).IsTrue();
        }
    }

    /// <summary>A pending signature is stamped by the timestamp authority when it is completed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompletedSignatureCarriesATimestamp(CancellationToken cancellationToken)
    {
        var certificate = SignatureFixtures.Signer;
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, Options() with { Timestamp = new TimestamperClient(authority) }, Identity(certificate), cancellationToken);
        using var key = certificate.GetRSAPrivateKey()!;
        var signed = await PdfDocumentSigning.CompleteAsync(pending, key.SignData(pending.SignedAttributes, DefaultHash, RSASignaturePadding.Pkcs1), cancellationToken);
        var report = SignatureFixtures.Validate(signed.Bytes, certificate)[0];

        using (Assert.Multiple())
        {
            await Assert.That(authority.Issued).IsEqualTo(1);
            await Assert.That(report.HasTimestamp).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
        }
    }

    /// <summary>A signature value too big for the room reserved is refused with a message that says how much room to ask for.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedSignatureIsRefused(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, Options(), Identity(SignatureFixtures.Signer), cancellationToken);

        var failure = await Assert.ThrowsAsync<PdfException>(async () => await PdfDocumentSigning.CompleteAsync(pending, new byte[OversizedSignature], cancellationToken));

        await Assert.That(failure!.Message).Contains("Headroom");
    }

    /// <summary>Preparing a signature leaves the document and the pending file apart: the source bytes are not changed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparingLeavesTheSourceUnchanged(CancellationToken cancellationToken)
    {
        var source = SignatureFixtures.Blank();
        var before = source.ToArray();
        using var document = PdfDocumentReader.Open(source, null);
        _ = await PdfDocumentSigning.PrepareAsync(document, Options(), Identity(SignatureFixtures.Signer), cancellationToken);

        await Assert.That(source).IsEquivalentTo(before);
    }

    /// <summary>Gets the options every test uses: a fixed signing time keeps the file repeatable.</summary>
    /// <returns>The options.</returns>
    private static PdfSigningOptions Options() => new() { Reason = Approved, SigningTime = SigningFixtures.Time };

    /// <summary>Gets the identity of a certificate.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The identity.</returns>
    private static PdfSignerIdentity Identity(X509Certificate2 certificate) => new([certificate.RawData], PdfSignatureAlgorithm.RsaPkcs1);

    /// <summary>Prepares a signature and completes it with a value signed with the certificate's key.</summary>
    /// <param name="source">The file.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The signed file.</returns>
    private static async Task<PdfSigningResult> PrepareAndCompleteAsync(byte[] source, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(source, null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, Options(), Identity(certificate), cancellationToken);
        using var key = certificate.GetRSAPrivateKey()!;
        return await PdfDocumentSigning.CompleteAsync(pending, key.SignData(pending.SignedAttributes, DefaultHash, RSASignaturePadding.Pkcs1), cancellationToken);
    }

    /// <summary>A signer that keeps the key to itself, as a token or remote service does.</summary>
    private sealed class RemoteSigner : IPdfSignatureProvider
    {
        /// <summary>The certificate holding the private key.</summary>
        private readonly X509Certificate2 _certificate;

        /// <summary>Initializes a new instance of the <see cref="RemoteSigner"/> class.</summary>
        /// <param name="certificate">The certificate holding the private key.</param>
        internal RemoteSigner(X509Certificate2 certificate) => _certificate = certificate;

        /// <inheritdoc/>
        public IReadOnlyList<byte[]> Certificates => [_certificate.RawData];

        /// <inheritdoc/>
        public PdfSignatureAlgorithm Algorithm => PdfSignatureAlgorithm.RsaPkcs1;

        /// <summary>Gets the times the signer was asked to sign.</summary>
        internal int Calls { get; private set; }

        /// <inheritdoc/>
        public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, PdfDigestMethod digest, CancellationToken cancellationToken)
        {
            Calls++;
            using var key = _certificate.GetRSAPrivateKey()!;
            return ValueTask.FromResult(key.SignData(data.Span, DefaultHash, RSASignaturePadding.Pkcs1));
        }
    }
}
