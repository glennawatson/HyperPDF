// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for the public-key security handler (<c>Adobe.PubSec</c>).</summary>
public sealed class PublicKeyTests
{
    /// <summary>The permissions written into the envelopes: printing and copying denied.</summary>
    private const int Permissions = -3904;

    /// <summary>More recipients than the handler reads.</summary>
    private const int TooManyRecipients = 300;

    /// <summary>Gets the recipient, made once because RSA key generation is slow.</summary>
    private static X509Certificate2 Recipient { get; } = PdfSigning.CreateCertificate("First Recipient", null, false, TimeProvider.System);

    /// <summary>Gets a second recipient.</summary>
    private static X509Certificate2 OtherRecipient { get; } = PdfSigning.CreateCertificate("Other Recipient", null, false, TimeProvider.System);

    /// <summary>Each cipher opens with the recipient's certificate, gives the plain content and the enveloped permissions.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PublicKeyCipher.Rc4)]
    [Arguments(PublicKeyCipher.Aes128)]
    [Arguments(PublicKeyCipher.Aes256)]
    public async Task OpensWithCertificate(PublicKeyCipher cipher)
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.Encrypted(cipher, Permissions, true, Recipient), Recipient);

        await Assert.That(document.IsEncrypted).IsTrue();
        await Assert.That(document.Objects.Security!.Permissions).IsEqualTo(Permissions);
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>With metadata left unencrypted, the key derivation adds the 0xFFFFFFFF marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnencryptedMetadataChangesTheKey()
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.Encrypted(PublicKeyCipher.Aes256, Permissions, false, Recipient), Recipient);

        await Assert.That(document.Objects.Security!.EncryptMetadata).IsFalse();
        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>The second of two recipients opens the document too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondRecipientOpens()
    {
        using var document = PdfDocumentReader.OpenWithCertificate(PublicKeyPdf.Encrypted(PublicKeyCipher.Aes128, Permissions, true, Recipient, OtherRecipient), OtherRecipient);

        await Assert.That(Content(document)).IsEqualTo(PublicKeyPdf.Content);
    }

    /// <summary>The decrypted document renders the same pixels as its plaintext twin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersLikePlaintextTwin()
    {
        using var encrypted = PdfDocumentReader.OpenWith(
            PublicKeyPdf.Encrypted(PublicKeyCipher.Aes256, Permissions, true, Recipient),
            new PdfOpenOptions { Certificate = Recipient, DrawingServices = SkiaTestRegistration.CreateServices() });
        using var plain = SkiaTestRegistration.Open(PublicKeyPdf.Plain(), null);

        await Assert.That(Render(encrypted)).IsEquivalentTo(Render(plain));
    }

    /// <summary>Opening without a certificate gives a clear certificate error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoCertificateIsAClearError()
    {
        var file = PublicKeyPdf.Encrypted(PublicKeyCipher.Aes256, Permissions, true, Recipient);
        var exception = await Assert.That(() => SkiaTestRegistration.Open(file, null)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
    }

    /// <summary>A certificate that is not a recipient gives the same error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongCertificateIsAClearError()
    {
        var file = PublicKeyPdf.Encrypted(PublicKeyCipher.Aes256, Permissions, true, Recipient);
        var exception = await Assert.That(() => PdfDocumentReader.OpenWithCertificate(file, OtherRecipient)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
    }

    /// <summary>Malformed /Recipients entries are rejected as a damaged dictionary.</summary>
    /// <param name="recipients">The /Recipients value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[]")]
    [Arguments("[1 2]")]
    [Arguments("[()]")]
    [Arguments("/NotAList")]
    public async Task MalformedRecipientsAreRejected(string recipients)
    {
        var file = PublicKeyPdf.WithDictionary($"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s4 /V 2 /Length 128 /Recipients {recipients} >>");
        var exception = await Assert.That(() => PdfDocumentReader.OpenWithCertificate(file, Recipient)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Format);
    }

    /// <summary>More recipients than the limit are rejected without decrypting any.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TooManyRecipientsAreRejected()
    {
        var list = new StringBuilder();
        for (var i = 0; i < TooManyRecipients; i++)
        {
            _ = list.Append("<30> ");
        }

        var file = PublicKeyPdf.WithDictionary($"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s4 /V 2 /Length 128 /Recipients [{list}] >>");
        var exception = await Assert.That(() => PdfDocumentReader.OpenWithCertificate(file, Recipient)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Format);
    }

    /// <summary>Damaged recipients that are strings are skipped, so the certificate is reported as not a recipient.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GarbageRecipientIsSkipped()
    {
        var file = PublicKeyPdf.WithDictionary("<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s4 /V 2 /Length 128 /Recipients [<3003020101>] >>");
        var exception = await Assert.That(() => PdfDocumentReader.OpenWithCertificate(file, Recipient)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
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
