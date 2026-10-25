// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for metadata streams and named crypt filters when a file is read.</summary>
public sealed class StreamCryptTests
{
    /// <summary>The object number of the stream under test.</summary>
    private const int StreamNumber = 4;

    /// <summary>The object number of the /Encrypt dictionary.</summary>
    private const int EncryptNumber = 3;

    /// <summary>The /Size of the sample files.</summary>
    private const int FileSize = 5;

    /// <summary>The catalog object.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>Gets the text stored in the stream under test.</summary>
    private static ReadOnlySpan<byte> PlainText => "<?xpacket begin?><x:xmpmeta/>"u8;

    /// <summary>A metadata stream is left in the clear when /EncryptMetadata is false, and decrypted when it is true.</summary>
    /// <param name="encryptMetadata">The /EncryptMetadata value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MetadataStreamFollowsEncryptMetadata(bool encryptMetadata)
    {
        using var setup = EncryptionSetup.Revision4(encryptMetadata, new());
        var plain = PlainText.ToArray();
        var stored = encryptMetadata ? setup.Handler.EncryptStream(Id(), plain) : plain;
        var file = FileWith(setup, "/Type /Metadata /Subtype /XML", stored);
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>A stream with an ordinary type is decrypted even when /EncryptMetadata is false.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryStreamIsDecryptedWhenMetadataIsPlain()
    {
        using var setup = EncryptionSetup.Revision4(false, new());
        var plain = PlainText.ToArray();
        var file = FileWith(setup, string.Empty, setup.Handler.EncryptStream(Id(), plain));
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>A /Crypt filter naming Identity leaves the stream unencrypted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentityCryptFilterLeavesStreamInTheClear()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        var plain = PlainText.ToArray();
        var file = FileWith(setup, "/Filter /Crypt /DecodeParms << /Type /CryptFilterDecodeParms /Name /Identity >>", plain);
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>A /Crypt filter without a name is Identity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnnamedCryptFilterLeavesStreamInTheClear()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        var plain = PlainText.ToArray();
        var file = FileWith(setup, "/Filter [/Crypt]", plain);
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>A /Crypt filter naming a V2 filter decrypts with RC4, though the default stream filter is AES.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedCryptFilterUsesItsOwnMethod()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        var plain = PlainText.ToArray();
        var file = FileWith(setup, "/Filter [/Crypt] /DecodeParms [<< /Name /Other >>]", setup.EncryptRc4(Id(), plain));
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>A /Crypt filter naming the default AES filter decrypts with AES.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedCryptFilterCanNameTheDefaultFilter()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        var plain = PlainText.ToArray();
        var file = FileWith(setup, "/Filter /Crypt /DecodeParms << /Name /StdCF >>", setup.Handler.EncryptStream(Id(), plain));
        await Assert.That(Read(file)).IsEquivalentTo(plain);
    }

    /// <summary>Gets the id of the stream under test.</summary>
    /// <returns>The id.</returns>
    private static PdfObjectId Id() => new(StreamNumber, 0);

    /// <summary>Builds an encrypted file with one stream.</summary>
    /// <param name="setup">The encryption.</param>
    /// <param name="entries">The stream's dictionary entries.</param>
    /// <param name="stored">The bytes as stored in the file.</param>
    /// <returns>The file.</returns>
    private static byte[] FileWith(
        EncryptionSetup setup,
        string entries,
        byte[] stored) =>
        new RawPdf().Object(
        1,
        Catalog).Object(
        PagesNumber,
        Pages).Object(
        EncryptNumber,
        setup.EncryptText).Stream(
        StreamNumber,
        entries,
        stored).Table(
        FileSize,
        $"/Root 1 0 R /Encrypt {EncryptNumber} 0 R /ID [<{setup.FileIdHex}> <{setup.FileIdHex}>]").ToArray();

    /// <summary>Opens a file and decodes the stream under test.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Read(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return StoreReading.GetObject(store, Id()).AsStream()!.DecodeToArray();
    }
}
