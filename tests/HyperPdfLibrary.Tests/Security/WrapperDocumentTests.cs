// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Syntax;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for unencrypted wrapper documents (ISO 32000-2 7.6.7).</summary>
public sealed class WrapperDocumentTests
{
    /// <summary>The payload's file name.</summary>
    private const string PayloadName = "payload.pdf";

    /// <summary>The cryptographic filter the payload names.</summary>
    private const string FilterName = "MicrosoftIRMServices";

    /// <summary>The password of the encrypted PDF inside the wrapper.</summary>
    private const string PayloadPassword = "open-sesame";

    /// <summary>The object number of the payload's encryption dictionary.</summary>
    private const int EncryptObject = 4;

    /// <summary>The object count including the free head.</summary>
    private const int PayloadObjectCount = 5;

    /// <summary>The page tree's object number.</summary>
    private const int PagesObject = 2;

    /// <summary>The page's object number.</summary>
    private const int PageObject = 3;

    /// <summary>The page tree shared by the wrapper and payload fixtures.</summary>
    private const string PageTree = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>A wrapper's payload is found with its file name, cryptographic filter and version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PayloadIsListed()
    {
        using var document = PdfDocumentReader.Open(Wrapper(), null);
        var payload = PdfDocumentWrapper.GetEncryptedPayload(document);

        await Assert.That(PdfDocumentWrapper.IsWrapperDocument(document)).IsTrue();
        await Assert.That(payload).IsNotNull();
        await Assert.That(payload!.FileName).IsEqualTo(PayloadName);
        await Assert.That(payload.CryptographicFilter).IsEqualTo(FilterName);
        await Assert.That(payload.Version).IsEqualTo("2");
        await Assert.That(payload.GetBytes()).IsEquivalentTo(Payload());
    }

    /// <summary>The payload opens as a document of its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PayloadOpens()
    {
        using var document = PdfDocumentReader.Open(Wrapper(), null);
        using var payload = PdfDocumentWrapper.OpenEncryptedPayload(document, null);

        await Assert.That(payload.PageCount).IsEqualTo(1);
    }

    /// <summary>A wrapper read from a stream opens its embedded PDF through the async API.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PayloadOpensAsync()
    {
        await using var source = new MemoryStream(Wrapper());
        using var document = await PdfDocumentReader.OpenAsync(source, null, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        using var payload = await PdfDocumentWrapper.OpenEncryptedPayloadAsync(document, null, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.That(payload.PageCount).IsEqualTo(1);
    }

    /// <summary>The wrapper passes a password to the encrypted document inside it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordOpensEncryptedPayload()
    {
        await using var source = new MemoryStream(Wrapper(PasswordPayload()));
        using var document = await PdfDocumentReader.OpenAsync(source, null, CancellationToken.None);
        var locked = await Assert.That(() => PdfDocumentWrapper.OpenEncryptedPayload(document, null)).Throws<PdfException>();
        await Assert.That(locked!.Error).IsEqualTo(PdfError.Password);

        using var payload = await PdfDocumentWrapper.OpenEncryptedPayloadAsync(document, PayloadPassword, CancellationToken.None);
        await Assert.That(payload.PageCount).IsEqualTo(1);
    }

    /// <summary>A plain document is not a wrapper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainDocumentIsNotAWrapper()
    {
        using var document = PdfDocumentReader.Open(Payload(), null);

        await Assert.That(PdfDocumentWrapper.IsWrapperDocument(document)).IsFalse();
        await Assert.That(() => PdfDocumentWrapper.OpenEncryptedPayload(document, null)).Throws<PdfException>();
    }

    /// <summary>Builds the payload: a one-page document.</summary>
    /// <returns>The file.</returns>
    private static byte[] Payload() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        PageTree,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");

    /// <summary>Builds a one-page PDF with revision 5 password encryption.</summary>
    /// <returns>The encrypted PDF.</returns>
    private static byte[] PasswordPayload()
    {
        var encryption = EncryptionSetup.Revision5Text(PayloadPassword, out _);
        const string id = "<00112233445566778899AABBCCDDEEFF>";
        return new RawPdf()
            .Object(1, "<< /Type /Catalog /Pages 2 0 R >>")
            .Object(PagesObject, PageTree)
            .Object(PageObject, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>")
            .Object(EncryptObject, encryption)
            .Table(PayloadObjectCount, $"/Root 1 0 R /Encrypt {EncryptObject} 0 R /ID [{id} {id}]")
            .ToArray();
    }

    /// <summary>Builds a wrapper: a collection whose associated file is the payload.</summary>
    /// <returns>The file.</returns>
    private static byte[] Wrapper() => Wrapper(Payload());

    /// <summary>Builds a wrapper around the supplied PDF.</summary>
    /// <param name="payload">The embedded PDF bytes.</param>
    /// <returns>The wrapper.</returns>
    private static byte[] Wrapper(byte[] payload) => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Collection << /View /H >> /AF [4 0 R] /Names << /EmbeddedFiles << /Names [(payload.pdf) 4 0 R] >> >> >>",
        PageTree,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        $"<< /Type /Filespec /F ({PayloadName}) /UF ({PayloadName}) /AFRelationship /EncryptedPayload /EP << /Type /EncryptedPayload /Subtype /{FilterName} /Version (2) >> /EF << /F 5 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", Encoding.Latin1.GetString(payload)));
}
