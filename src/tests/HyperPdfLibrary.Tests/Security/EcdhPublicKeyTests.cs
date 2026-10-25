// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for public-key recipients that use ephemeral-static ECDH key agreement, from OpenSSL's envelopes.</summary>
public sealed class EcdhPublicKeyTests
{
    /// <summary>The permissions in the RSA recipient's list: printing allowed, copying denied.</summary>
    private const int RsaPermissions = -1852;

    /// <summary>The bytes from a P-256 envelope's ephemeral point marker to a byte inside the point.</summary>
    private const int PointByte = 10;

    /// <summary>The bytes before the EncryptedContentInfo where the wrapped content key starts.</summary>
    private const int WrappedKeyStart = 40;

    /// <summary>The bytes cut from the end of an envelope to truncate it.</summary>
    private const int TruncatedBytes = 10;

    /// <summary>Gets the P-256 recipient.</summary>
    private static X509Certificate2 P256 { get; } = EcdhRecipients.P256();

    /// <summary>Gets the P-384 recipient.</summary>
    private static X509Certificate2 P384 { get; } = EcdhRecipients.P384();

    /// <summary>Gets an RSA recipient, made once because RSA key generation is slow.</summary>
    private static X509Certificate2 RsaRecipient { get; } = PdfSigning.CreateCertificate("RSA Recipient", null, false, TimeProvider.System);

    /// <summary>Gets the start of the envelopes' EncryptedContentInfo: a SEQUENCE then the id-data OID.</summary>
    private static byte[] EncryptedContentInfoStart { get; } = [0x30, 0x4C, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x01];

    /// <summary>Gets the start of a P-256 ephemeral key's BIT STRING: tag, length, no unused bits, uncompressed.</summary>
    private static byte[] P256PointStart { get; } = [0x03, 0x42, 0x00, 0x04];

    /// <summary>Each document cipher opens with the P-256 recipient and gives its content and permissions.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PublicKeyCipher.Rc4)]
    [Arguments(PublicKeyCipher.Aes128)]
    [Arguments(PublicKeyCipher.Aes256)]
    public async Task OpensP256Recipient(PublicKeyCipher cipher)
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.FromEnvelopes(cipher, true, EcdhRecipients.P256Default), P256);

        await Assert.That(document.IsEncrypted).IsTrue();
        await Assert.That(document.Objects.Security!.Permissions).IsEqualTo(EcdhRecipients.PrintingDenied);
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>A P-384 recipient named by subject key identifier, with a SHA-384 KDF and AES-128, opens.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensP384RecipientBySubjectKeyIdentifier()
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, EcdhRecipients.P384KeyId), P384);

        await Assert.That(document.Objects.Security!.Permissions).IsEqualTo(EcdhRecipients.AllAllowed);
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>Either recipient of one shared list opens it, with cofactor ECDH, a SHA-256 KDF and AES-192.</summary>
    /// <param name="useP384">Whether to open with the P-384 recipient.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EitherRecipientOfASharedListOpens(bool useP384)
    {
        var file = PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes128, true, EcdhRecipients.BothCofactor);
        using var document = PdfDocumentReader.OpenWithCertificate(file, useP384 ? P384 : P256);

        await Assert.That(document.Objects.Security!.Permissions).IsEqualTo(EcdhRecipients.PrintingDenied);
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>With metadata left unencrypted, the ECDH recipient's seed still gives the right key.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnencryptedMetadataOpens()
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, false, EcdhRecipients.P256Default), P256);

        await Assert.That(document.Objects.Security!.EncryptMetadata).IsFalse();
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>The decrypted document renders the same pixels as its plaintext twin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersLikePlaintextTwin()
    {
        using var encrypted = PdfDocumentReader.OpenWith(
            PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, EcdhRecipients.P256Default),
            new PdfOpenOptions { Certificate = P256, DrawingServices = SkiaTestRegistration.CreateServices() });
        using var plain = SkiaTestRegistration.Open(PublicKeyPdf.Plain(), null);

        await Assert.That(Render(encrypted)).IsEquivalentTo(Render(plain));
    }

    /// <summary>RSA and ECDH recipients in separate lists each get their own list's permissions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EachRecipientGetsItsListsPermissions()
    {
        var file = PublicKeyPdf.FromEnvelopes(
            PublicKeyCipher.Aes256,
            true,
            PublicKeyPdf.Envelope(RsaPermissions, RsaRecipient),
            EcdhRecipients.P256Default,
            EcdhRecipients.P384KeyId);

        await Assert.That(Permissions(file, RsaRecipient)).IsEqualTo(RsaPermissions);
        await Assert.That(Permissions(file, P256)).IsEqualTo(EcdhRecipients.PrintingDenied);
        await Assert.That(Permissions(file, P384)).IsEqualTo(EcdhRecipients.AllAllowed);
    }

    /// <summary>A recipient in two lists gets the permissions of the first list (ISO 32000-2 Table 23).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstMatchingListGivesThePermissions()
    {
        var sharedFirst = PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, EcdhRecipients.BothCofactor, EcdhRecipients.P384KeyId);
        var ownFirst = PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, EcdhRecipients.P384KeyId, EcdhRecipients.BothCofactor);

        await Assert.That(Permissions(sharedFirst, P384)).IsEqualTo(EcdhRecipients.PrintingDenied);
        await Assert.That(Permissions(ownFirst, P384)).IsEqualTo(EcdhRecipients.AllAllowed);
    }

    /// <summary>An elliptic curve certificate that is not a recipient is reported as such.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongCertificateIsNotARecipient()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ECDH P-256 Recipient", key, HashAlgorithmName.SHA256);
        var now = TimeProvider.System.GetUtcNow();
        using var stranger = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));

        await Assert.That(Result(stranger, EcdhRecipients.P256Default)).IsEqualTo(PdfSecurityResult.CertificateRequired);
        await AssertOpenFails(stranger, EcdhRecipients.P256Default);
    }

    /// <summary>The recipient's certificate without its private key gives a missing-key result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPrivateKeyIsReported()
    {
        using var certificate = EcdhRecipients.P256WithoutKey();

        await Assert.That(Result(certificate, EcdhRecipients.P256Default)).IsEqualTo(PdfSecurityResult.PrivateKeyRequired);
        await AssertOpenFails(certificate, EcdhRecipients.P256Default);
    }

    /// <summary>A recipient for the certificate that uses the Triple DES key wrap is reported as unsupported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedAlgorithmsAreReported()
    {
        await Assert.That(Result(P256, EcdhRecipients.P256TripleDes)).IsEqualTo(PdfSecurityResult.UnsupportedRecipient);
        await AssertOpenFails(P256, EcdhRecipients.P256TripleDes);
    }

    /// <summary>A truncated envelope, a damaged wrapped key and a point off the curve give a damaged result.</summary>
    /// <param name="damage">The damage.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(EnvelopeDamage.Truncated)]
    [Arguments(EnvelopeDamage.WrappedKey)]
    [Arguments(EnvelopeDamage.EphemeralPoint)]
    public async Task DamagedEnvelopesAreReported(EnvelopeDamage damage)
    {
        var envelope = Damage(EcdhRecipients.P256Default, damage);

        await Assert.That(Result(P256, envelope)).IsEqualTo(PdfSecurityResult.RecipientDamaged);
        await AssertOpenFails(P256, envelope);
    }

    /// <summary>A list that cannot be read does not hide a later list for the certificate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedListBeforeAGoodOneIsSkipped()
    {
        var file = PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, Damage(EcdhRecipients.P256Default, EnvelopeDamage.Truncated), EcdhRecipients.P256Default);
        using var document = PdfDocumentReader.OpenWithCertificate(file, P256);

        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>An RSA certificate is not taken for an ECDH recipient, and an ECDH certificate not for an RSA one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeyTypesDoNotCross()
    {
        await Assert.That(Result(RsaRecipient, EcdhRecipients.P256Default)).IsEqualTo(PdfSecurityResult.CertificateRequired);
        await Assert.That(Result(P256, PublicKeyPdf.Envelope(RsaPermissions, RsaRecipient))).IsEqualTo(PdfSecurityResult.CertificateRequired);
    }

    /// <summary>Opens a document and reads its permissions.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The permissions.</returns>
    private static int Permissions(byte[] file, X509Certificate2 certificate)
    {
        using var document = PdfDocumentReader.OpenWithCertificate(file, certificate);
        return document.Objects.Security!.Permissions;
    }

    /// <summary>Creates the public-key handler for one recipient list and returns the outcome.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="envelope">The recipient list.</param>
    /// <returns>The outcome.</returns>
    private static PdfSecurityResult Result(X509Certificate2 certificate, byte[] envelope)
    {
        var text = PublicKeyPdf.EncryptDictionary(PublicKeyCipher.Aes256, [envelope], true);
        var encrypt = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, new()).ParseValue().AsDictionary()!;
        var result = PdfSecurityHandler.TryCreatePublicKey(encrypt, certificate, out var handler);
        handler?.Dispose();
        return result;
    }

    /// <summary>Checks that opening fails with a certificate error rather than a crash.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="envelope">The recipient list.</param>
    /// <returns>A task.</returns>
    private static async Task AssertOpenFails(X509Certificate2 certificate, byte[] envelope)
    {
        var file = PublicKeyPdf.FromEnvelopes(PublicKeyCipher.Aes256, true, envelope);
        var exception = await Assert.That(() => PdfDocumentReader.OpenWithCertificate(file, certificate)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
    }

    /// <summary>Damages a copy of a P-256 envelope.</summary>
    /// <param name="envelope">The envelope.</param>
    /// <param name="damage">The damage.</param>
    /// <returns>The damaged copy.</returns>
    private static byte[] Damage(byte[] envelope, EnvelopeDamage damage)
    {
        if (damage == EnvelopeDamage.Truncated)
        {
            return envelope[..^TruncatedBytes];
        }

        var copy = envelope.ToArray();
        var index = damage == EnvelopeDamage.WrappedKey
            ? copy.AsSpan().IndexOf(EncryptedContentInfoStart) - WrappedKeyStart
            : copy.AsSpan().IndexOf(P256PointStart) + PointByte;
        copy[index] ^= 1;
        return copy;
    }

    /// <summary>Decodes the page's content stream.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The content as text.</returns>
    private static string Content(PdfDocument document) =>
        Encoding.ASCII.GetString(PdfDocumentPages.GetPage(document, 0).Dictionary.GetStream(KnownName.Contents)!.DecodeToArray());

    /// <summary>Renders the first page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(PdfDocument document)
    {
        using var renderer = new PdfPageRenderer(document);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1, out var width, out var height);
        var stride = width * sizeof(int);
        var pixels = new byte[stride * height];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, stride));
        return pixels;
    }
}
