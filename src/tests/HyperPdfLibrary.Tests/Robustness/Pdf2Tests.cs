// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>PDF 2.0 features: /Version 2.0, UTF-8 text strings, output intents and AES-256 revision 6 encryption.</summary>
public sealed class Pdf2Tests
{
    /// <summary>The user password of the protected document.</summary>
    private const string UserPassword = "user-secret";

    /// <summary>A password that opens nothing.</summary>
    private const string WrongPassword = "not-the-password";

    /// <summary>The page count of the generated source document.</summary>
    private const int PlainPages = 3;

    /// <summary>The revision 6 value used by AES-256 documents.</summary>
    private const int Revision6 = 6;

    /// <summary>The width of the PDF 2.0 text document's page.</summary>
    private const int TextPageWidth = 300;

    /// <summary>The height of the PDF 2.0 text document's page.</summary>
    private const int TextPageHeight = 400;

    /// <summary>The object number of the (invalid) output profile.</summary>
    private const int ProfileNumber = 9;

    /// <summary>A UTF-8 string with a byte order mark reads as text in the info dictionary, the outline and the version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Utf8TextStringsAndVersionRead()
    {
        using var document = PdfDocumentReader.Open(Pdf2Documents.CreateText(), null);
        var info = PdfDocumentMetadata.GetInfo(document);
        await Assert.That(info.Title).IsEqualTo(Pdf2Documents.HelloText);
        await Assert.That(info.Author).IsEqualTo("Jörn");
        await Assert.That(info.Version).IsEqualTo("2.0");
        await Assert.That(document.Objects.Version).IsEqualTo("2.0");
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Title).IsEqualTo(Pdf2Documents.HelloText);
    }

    /// <summary>The UTF-8 byte order mark is stripped and bad bytes after it do not throw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfTextDecodesUtf8WithByteOrderMark()
    {
        await Assert.That(PdfText.Decode([0xEF, 0xBB, 0xBF, 0x41, 0xE2, 0x82, 0xAC])).IsEqualTo("A€");
        await Assert.That(PdfText.Decode([0xEF, 0xBB, 0xBF])).IsEqualTo(string.Empty);
        await Assert.That(PdfText.Decode([0xEF, 0xBB, 0xBF, 0x41, 0xFF, 0x42])).StartsWith("A");
    }

    /// <summary>Output intents on the catalog and on a page, pointing at a profile that is not one, do not disturb reading.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutputIntentsAreIgnoredSafely()
    {
        using var document = PdfDocumentReader.Open(Pdf2Documents.CreateText(), null);
        var page = PdfDocumentPages.GetPage(document, 0);
        var content = WritingTestDocuments.PageContents(document.Objects);
        var profile = StoreReading.GetObject(document.Objects, new(ProfileNumber, 0)).AsStream()!;
        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(page.Width).IsEqualTo(TextPageWidth);
        await Assert.That(page.Height).IsEqualTo(TextPageHeight);
        await Assert.That(Encoding.Latin1.GetString(content[0])).IsEqualTo(Pdf2Documents.ContentText);
        await Assert.That(profile.DecodeToArray().Length).IsGreaterThan(0);
        await Assert.That(DocumentExerciser.Read(document)).IsNotEmpty();
    }

    /// <summary>An AES-256 document with an empty user password opens and reads like the plain one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Aes256DocumentOpensAndMatchesPlain()
    {
        var plain = RobustnessSeeds.Create()[0].Bytes;
        var encrypted = Pdf2Documents.EncryptAes256(plain, string.Empty, RobustnessSeeds.OwnerPassword);
        using var plainDocument = PdfDocumentReader.Open(plain, null);
        using var document = PdfDocumentReader.Open(encrypted, null);
        await Assert.That(document.IsEncrypted).IsTrue();
        await Assert.That(document.Objects.Security!.Revision).IsEqualTo(Revision6);
        await Assert.That(document.Objects.Version).IsEqualTo("2.0");

        // The only difference the reader sees is the header version the rewrite changed.
        await Assert.That(DocumentExerciser.Read(document)).IsEqualTo(DocumentExerciser.Read(plainDocument).Replace("|1.7;", "|2.0;", StringComparison.Ordinal));
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(PdfDocumentMetadata.GetInfo(plainDocument).Title);
    }

    /// <summary>The user and owner passwords open an AES-256 document; no password and a wrong one report a password error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Aes256PasswordsAreChecked()
    {
        var plain = RobustnessSeeds.Create()[0].Bytes;
        var encrypted = Pdf2Documents.EncryptAes256(plain, UserPassword, RobustnessSeeds.OwnerPassword);
        using var asUser = PdfDocumentReader.Open(encrypted, UserPassword);
        using var asOwner = PdfDocumentReader.Open(encrypted, RobustnessSeeds.OwnerPassword);
        var none = await Assert.That(() => PdfDocumentReader.Open(encrypted, null)).Throws<PdfException>();
        var wrong = await Assert.That(() => PdfDocumentReader.Open(encrypted, WrongPassword)).Throws<PdfException>();
        await Assert.That(asUser.PageCount).IsEqualTo(PlainPages);
        await Assert.That(asOwner.PageCount).IsEqualTo(PlainPages);
        await Assert.That(none!.Error).IsEqualTo(PdfError.Password);
        await Assert.That(wrong!.Error).IsEqualTo(PdfError.Password);
    }

    /// <summary>A PDF 2.0 encrypted document with UTF-8 strings decrypts them to the right text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Aes256DecryptsUtf8Strings()
    {
        var encrypted = Pdf2Documents.EncryptAes256(Pdf2Documents.CreateText(), string.Empty, RobustnessSeeds.OwnerPassword);
        using var document = PdfDocumentReader.Open(encrypted, null);
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(Pdf2Documents.HelloText);
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Title).IsEqualTo(Pdf2Documents.HelloText);
    }

    /// <summary>Revision 6 allows AES-256 only: RC4 and AES-128 crypt filters are refused as unsupported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Revision6RefusesRc4AndAes128()
    {
        var aes256 = Create("/CFM /AESV3");
        var rc4 = Create("/CFM /V2");
        var aes128 = Create("/CFM /AESV2");
        await Assert.That(PdfSecurityHandler.TryCreate(aes256, Pdf2Documents.FirstId(), null, out var handler)).IsEqualTo(PdfSecurityResult.Success);
        handler!.Dispose();
        await Assert.That(PdfSecurityHandler.TryCreate(rc4, Pdf2Documents.FirstId(), null, out _)).IsEqualTo(PdfSecurityResult.UnsupportedHandler);
        await Assert.That(PdfSecurityHandler.TryCreate(aes128, Pdf2Documents.FirstId(), null, out _)).IsEqualTo(PdfSecurityResult.UnsupportedHandler);
    }

    /// <summary>Parses a revision 6 /Encrypt dictionary with a crypt filter method.</summary>
    /// <param name="method">The method entry.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Create(string method)
    {
        var text = Pdf2Documents.EncryptText(string.Empty, RobustnessSeeds.OwnerPassword, method);
        var parser = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, new());
        return parser.ParseValue().AsDictionary()!;
    }
}
