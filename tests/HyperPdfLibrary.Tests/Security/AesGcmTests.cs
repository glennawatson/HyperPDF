// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for reading and saving AES-GCM (ISO/TS 32003:2023) files made by an independent builder.</summary>
public sealed class AesGcmTests
{
    /// <summary>The number of encryptions checked for repeated IVs.</summary>
    private const int IvSamples = 5_000;

    /// <summary>The offset of the first cipher text byte, after the IV.</summary>
    private const int CipherOffset = AesGcmPdf.IvLength;

    /// <summary>The hexadecimal digits that spell a byte.</summary>
    private const int HexDigitsPerByte = 2;

    /// <summary>The samples that alternate between encrypting a stream and a string.</summary>
    private const int AlternatingSamples = 2;

    /// <summary>The encryptions left before the limit when the count is checked.</summary>
    private const int EncryptionsLeft = 2;

    /// <summary>The /V of ISO/TS 32003.</summary>
    private const int StandardVersion = 6;

    /// <summary>The /R of ISO/TS 32003.</summary>
    private const int StandardRevision = 7;

    /// <summary>Skips a test on a platform whose cryptography library has no AES-GCM, where the library reports the file as unsupported.</summary>
    [Before(Test)]
    public void RequireAesGcm()
    {
        if (!AesGcm.IsSupported)
        {
            Skip.Test("AES-GCM is not available on this platform.");
        }
    }

    /// <summary>The user password opens the file and the content, title and embedded file read back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UserPasswordOpensFile()
    {
        using var store = StoreOpening.Open(AesGcmPdf.Create(AesGcmLayout.Everything, true), AesGcmPdf.UserPassword);
        await Assert.That(Encoding.Latin1.GetString(ReadStream(store, AesGcmPdf.ContentNumber))).IsEqualTo(AesGcmPdf.ContentText);
        await Assert.That(ReadTitle(store)).IsEqualTo(AesGcmPdf.TitleText);
        await Assert.That(Encoding.Latin1.GetString(ReadStream(store, AesGcmPdf.AttachmentNumber))).IsEqualTo(AesGcmPdf.AttachmentText);
        await Assert.That(store.Security!.Revision).IsEqualTo(StandardRevision);
        await Assert.That(store.Context!.Snapshot()).IsEmpty();
    }

    /// <summary>The owner password opens the file too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerPasswordOpensFile()
    {
        using var store = StoreOpening.Open(AesGcmPdf.Create(AesGcmLayout.Everything, true), AesGcmPdf.OwnerPassword);
        await Assert.That(ReadTitle(store)).IsEqualTo(AesGcmPdf.TitleText);
    }

    /// <summary>A wrong or missing password is refused.</summary>
    /// <param name="password">The password.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("wrong")]
    [Arguments("")]
    [Arguments(null)]
    public async Task WrongPasswordIsRefused(string? password)
    {
        var file = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        var exception = await Assert.That(() => StoreOpening.Open(file, password)).Throws<PdfException>();
        await Assert.That(exception!.Error).IsEqualTo(PdfError.Password);
    }

    /// <summary>With only embedded files protected, the embedded file decrypts through /EFF and other data stays plain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedFileFilterProtectsOnlyEmbeddedFiles()
    {
        var file = AesGcmPdf.Create(AesGcmLayout.EmbeddedFilesOnly, true);
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        await Assert.That(Encoding.Latin1.GetString(ReadStream(store, AesGcmPdf.AttachmentNumber))).IsEqualTo(AesGcmPdf.AttachmentText);
        await Assert.That(Encoding.Latin1.GetString(ReadStream(store, AesGcmPdf.ContentNumber))).IsEqualTo(AesGcmPdf.ContentText);
        await Assert.That(ReadTitle(store)).IsEqualTo(AesGcmPdf.TitleText);
        await Assert.That(file.AsSpan().IndexOf(Encoding.Latin1.GetBytes(AesGcmPdf.AttachmentText))).IsEqualTo(-1);
    }

    /// <summary>A changed cipher text byte, tag byte or IV byte reads as no data and is reported, never as garbage.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="part">Which part of the attachment to change.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(AesGcmLayout.Everything, "cipher")]
    [Arguments(AesGcmLayout.Everything, "tag")]
    [Arguments(AesGcmLayout.Everything, "iv")]
    [Arguments(AesGcmLayout.EmbeddedFilesOnly, "cipher")]
    [Arguments(AesGcmLayout.EmbeddedFilesOnly, "tag")]
    public async Task ChangedStreamIsNotReturned(AesGcmLayout layout, string part)
    {
        var file = AesGcmPdf.Create(layout, true);
        var start = AesGcmPdf.Find(file, AesGcmPdf.IvFor(AesGcmPdf.AttachmentNumber));
        var length = AesGcmPdf.IvLength + AesGcmPdf.AttachmentText.Length + AesGcmPdf.TagLength;
        var offset = part switch
        {
            "iv" => start,
            "cipher" => start + CipherOffset,
            _ => start + length - 1,
        };
        file[offset] ^= 1;
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        var data = ReadStream(store, AesGcmPdf.AttachmentNumber);
        await Assert.That(data).IsEmpty();
        await Assert.That(store.Context!.Snapshot().Any(static d => d.Code == PdfDiagnosticCode.AuthenticationFailed && d.ObjectNumber == AesGcmPdf.AttachmentNumber)).IsTrue();
        await Assert.That(store.Context.HasRepairs).IsTrue();
    }

    /// <summary>A changed string reads as an empty string and is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedStringIsNotReturned()
    {
        var file = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        var hex = Encoding.ASCII.GetBytes(Convert.ToHexString(AesGcmPdf.Protect(AesGcmPdf.InfoNumber, Encoding.Latin1.GetBytes(AesGcmPdf.TitleText))));
        var at = AesGcmPdf.Find(file, hex);

        // Change the first cipher text digit, after the 12 IV bytes (24 hexadecimal digits).
        var digit = at + (CipherOffset * HexDigitsPerByte);
        file[digit] = file[digit] == (byte)'0' ? (byte)'1' : (byte)'0';
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        await Assert.That(ReadTitle(store)).IsEqualTo(string.Empty);
        await Assert.That(store.Context!.Snapshot().Any(static d => d.Code == PdfDiagnosticCode.AuthenticationFailed)).IsTrue();
    }

    /// <summary>Data shorter than the IV and tag reads as no data and is reported, without an exception.</summary>
    /// <param name="length">The stored length.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(27)]
    public async Task ShortPayloadIsBounded(int length)
    {
        var file = AesGcmPdf.Create(AesGcmLayout.Everything, true, new byte[length]);
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        var data = ReadStream(store, AesGcmPdf.AttachmentNumber);
        await Assert.That(data).IsEmpty();
    }

    /// <summary>A stream of exactly an IV and a tag is an authenticated empty stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyPlainTextRoundTrips()
    {
        using var setup = Handler();
        var stored = setup.EncryptStream(new(AesGcmPdf.ContentNumber, 0), []);
        await Assert.That(stored.Length).IsEqualTo(AesGcmCipher.Overhead);
        await Assert.That(setup.TryDecryptStream(new(AesGcmPdf.ContentNumber, 0), stored, out var plain)).IsTrue();
        await Assert.That(plain).IsEmpty();
    }

    /// <summary>The handler reports changed data as a failure instead of returning bytes, and the throwing form does not return them either.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlerRefusesChangedData()
    {
        using var handler = Handler();
        var id = new PdfObjectId(AesGcmPdf.ContentNumber, 0);
        var stored = handler.EncryptStream(id, Encoding.Latin1.GetBytes(AesGcmPdf.ContentText));
        stored[^1] ^= 1;
        await Assert.That(handler.TryDecryptStream(id, stored, out var plain)).IsFalse();
        await Assert.That(plain).IsNull();
        await Assert.That(handler.TryDecryptString(id, stored, out _)).IsFalse();
        _ = await Assert.That(() => handler.DecryptStream(id, stored)).Throws<PdfException>();
    }

    /// <summary>Every encryption uses a new IV, so equal data never encrypts to equal bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryEncryptionUsesANewIv()
    {
        using var handler = Handler();
        var id = new PdfObjectId(AesGcmPdf.ContentNumber, 0);
        var data = Encoding.Latin1.GetBytes(AesGcmPdf.ContentText);
        var ivs = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < IvSamples; i++)
        {
            var stored = i % AlternatingSamples == 0 ?handler.EncryptStream(id, data) : handler.EncryptString(id, data);
            _ = ivs.Add(Convert.ToHexString(stored.AsSpan(0, AesGcmPdf.IvLength)));
        }

        await Assert.That(ivs.Count).IsEqualTo(IvSamples);
    }

    /// <summary>Encrypted output follows the layout of ISO/TS 32003 5.2 and decrypts with an independent AES-GCM call.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedOutputHasIvCipherTextAndTag()
    {
        using var handler = Handler();
        var data = Encoding.Latin1.GetBytes(AesGcmPdf.AttachmentText);
        var stored = handler.EncryptStream(new(AesGcmPdf.AttachmentNumber, 0), data);
        await Assert.That(stored.Length).IsEqualTo(data.Length + AesGcmPdf.IvLength + AesGcmPdf.TagLength);
        var plain = new byte[data.Length];
        using var gcm = new AesGcm(AesGcmPdf.FileKey, AesGcmPdf.TagLength);
        gcm.Decrypt(stored.AsSpan(0, AesGcmPdf.IvLength), stored.AsSpan(AesGcmPdf.IvLength, data.Length), stored.AsSpan(AesGcmPdf.IvLength + data.Length), plain);
        await Assert.That(plain).IsEquivalentTo(data);
    }

    /// <summary>The size limit and the encryption count limit of one key are enforced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LimitsAreEnforced()
    {
        await Assert.That(AesGcmCipher.IsWithinSizeLimit(AesGcmCipher.MaxPlainLength)).IsTrue();
        await Assert.That(AesGcmCipher.IsWithinSizeLimit(AesGcmCipher.MaxPlainLength + 1)).IsFalse();
        var counter = AesGcmCipher.MaxEncryptions - EncryptionsLeft;
        await Assert.That(AesGcmCipher.TryCountEncryption(ref counter)).IsTrue();
        await Assert.That(AesGcmCipher.TryCountEncryption(ref counter)).IsTrue();
        await Assert.That(AesGcmCipher.TryCountEncryption(ref counter)).IsFalse();
    }

    /// <summary>Revision 7 needs version 6, an AESV4 filter and a valid /Perms; revision 6 cannot use AESV4.</summary>
    /// <param name="change">The text of the dictionary to change.</param>
    /// <param name="replacement">The text to put there.</param>
    /// <param name="expected">The expected outcome.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/V 6", "/V 5", PdfSecurityResult.UnsupportedHandler)]
    [Arguments("/R 7", "/R 6", PdfSecurityResult.UnsupportedHandler)]
    [Arguments("/CFM /AESV4", "/CFM /AESV3", PdfSecurityResult.Damaged)]
    [Arguments("/Perms <", "/Perms <00", PdfSecurityResult.Damaged)]
    [Arguments("/P -4", "/P -4", PdfSecurityResult.Success)]
    public async Task DictionaryRulesAreChecked(string change, string replacement, PdfSecurityResult expected)
    {
        var text = AesGcmPdf.EncryptText(AesGcmPdf.Create(AesGcmLayout.Everything, true)).Replace(change, replacement, StringComparison.Ordinal);
        var names = new PdfNameTable();
        var dictionary = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, names).ParseValue().AsDictionary()!;
        var result = PdfSecurityHandler.TryCreate(dictionary, [], AesGcmPdf.UserPassword, out var handler);
        handler?.Dispose();
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>A compact save keeps the file encrypted with AES-GCM: new IVs, the same /Encrypt entries and the extension.</summary>
    /// <param name="useObjectStreams">Whether the save packs objects into object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompactSaveKeepsGcmWithNewIvs(bool useObjectStreams)
    {
        var original = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        using var source = StoreOpening.Open(original, AesGcmPdf.UserPassword);
        var saved = PdfCompactWriter.Save(source, new(useObjectStreams, false));
        await Assert.That(Encoding.ASCII.GetString(saved, 0, "%PDF-2.0".Length)).IsEqualTo("%PDF-2.0");
        await AssertReopens(saved, true);
    }

    /// <summary>An incremental save of an edited file keeps AES-GCM, leaves the original bytes alone and encrypts the new object under a new IV.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveKeepsGcm()
    {
        var original = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        using var source = StoreOpening.Open(original, AesGcmPdf.UserPassword);
        var info = source.Trailer.GetDictionary(KnownName.Info)!.Clone();
        info.Set(source.Names.Intern("Subject"), PdfValue.FromString("edited subject"u8.ToArray()));
        StoreEditing.Replace(source, new(AesGcmPdf.InfoNumber, 0), PdfValue.FromDictionary(info));
        var saved = PdfIncrementalWriter.Save(source);
        await Assert.That(saved.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
        await AssertReopens(saved, false);
        using var reopened = StoreOpening.Open(saved, AesGcmPdf.UserPassword);
        await Assert.That(reopened.Trailer.GetDictionary(KnownName.Info)!.GetText(reopened.Names.Intern("Subject"))).IsEqualTo("edited subject");
        await Assert.That(ReadTitle(reopened)).IsEqualTo(AesGcmPdf.TitleText);
    }

    /// <summary>A save declares the extension when a GCM file lacks it, and a compact save raises an older header to 2.0.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SaveAddsMissingExtension(bool incremental)
    {
        var original = AesGcmPdf.Create(AesGcmLayout.Everything, false);
        using var source = StoreOpening.Open(original, AesGcmPdf.UserPassword);
        await Assert.That(AesGcmExtension.IsDeclared(source.Catalog, source.Names)).IsFalse();
        if (incremental)
        {
            StoreEditing.Replace(source, new(AesGcmPdf.InfoNumber, 0), PdfValue.FromDictionary(source.Trailer.GetDictionary(KnownName.Info)!.Clone()));
        }

        var saved = incremental ? PdfIncrementalWriter.Save(source) : PdfCompactWriter.Save(source, PdfCompactOptions.Classic);
        using var reopened = StoreOpening.Open(saved, AesGcmPdf.UserPassword);
        await Assert.That(AesGcmExtension.IsDeclared(reopened.Catalog, reopened.Names)).IsTrue();
        await Assert.That(reopened.Context!.Snapshot().Any(static d => d.Code == PdfDiagnosticCode.AuthenticationFailed)).IsFalse();
    }

    /// <summary>A stream that fails authentication stops a save, because writing it would hide the damage.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingChangedDataFails()
    {
        var file = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        file[AesGcmPdf.Find(file, AesGcmPdf.IvFor(AesGcmPdf.ContentNumber)) + CipherOffset] ^= 1;
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        _ = await Assert.That(() => PdfCompactWriter.Save(store, PdfCompactOptions.Classic)).Throws<PdfException>();
    }

    /// <summary>
    /// Writes the builder's files and the library's saved files to the folder named by <c>HYPERPDF_AESGCM_OUTPUT</c>, for checking
    /// with other PDF readers. It does nothing when the variable is not set.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesFilesForOtherReaders()
    {
        var folder = Environment.GetEnvironmentVariable("HYPERPDF_AESGCM_OUTPUT");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        var everything = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        await File.WriteAllBytesAsync(Path.Combine(folder, "builder-everything.pdf"), everything);
        await File.WriteAllBytesAsync(Path.Combine(folder, "builder-embedded-only.pdf"), AesGcmPdf.Create(AesGcmLayout.EmbeddedFilesOnly, true));
        using var store = StoreOpening.Open(everything, AesGcmPdf.UserPassword);
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-classic.pdf"), PdfCompactWriter.Save(store, PdfCompactOptions.Classic));
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-object-streams.pdf"), PdfCompactWriter.Save(store, PdfCompactOptions.Default));
    }

    /// <summary>The ISO_ extension in the builder's file is read back as level 32003.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtensionIsDeclaredByTheBuilder()
    {
        var file = AesGcmPdf.Create(AesGcmLayout.Everything, true);
        using var store = StoreOpening.Open(file, AesGcmPdf.UserPassword);
        await Assert.That(AesGcmExtension.IsDeclared(store.Catalog, store.Names)).IsTrue();
    }

    /// <summary>Opens a saved file and checks the encryption entries, the data, the extension and the IVs.</summary>
    /// <param name="saved">The saved file.</param>
    /// <param name="rewritten">Whether the save encrypted every stream again, so none keeps an IV of the original.</param>
    /// <returns>A task.</returns>
    private static async Task AssertReopens(byte[] saved, bool rewritten)
    {
        using var store = StoreOpening.Open(saved, AesGcmPdf.UserPassword);
        await Assert.That(Encoding.Latin1.GetString(ReadStream(store, ContentNumberAfter(store)))).IsEqualTo(AesGcmPdf.ContentText);
        await Assert.That(ReadTitle(store)).IsEqualTo(AesGcmPdf.TitleText);
        var encrypt = store.Trailer.GetDictionary(KnownName.Encrypt)!;
        await Assert.That(encrypt.GetInt32(KnownName.V)).IsEqualTo(StandardVersion);
        await Assert.That(encrypt.GetInt32(KnownName.R)).IsEqualTo(StandardRevision);
        await Assert.That(encrypt.GetDictionary(KnownName.CF)!.GetDictionary(KnownName.StdCF)!.IsName(KnownName.CFM, KnownName.AESV4)).IsTrue();
        await Assert.That(AesGcmExtension.IsDeclared(store.Catalog, store.Names)).IsTrue();
        await Assert.That(store.Context!.Snapshot().Any(static d => d.Code == PdfDiagnosticCode.AuthenticationFailed)).IsFalse();

        // No two streams share an IV. A compact save encrypts every stream again, so none keeps its original IV.
        var ivs = SavedStreamIvs(store);
        await Assert.That(ivs.Distinct().Count()).IsEqualTo(ivs.Count);
        foreach (var number in new[] { AesGcmPdf.ContentNumber, AesGcmPdf.AttachmentNumber })
        {
            await Assert.That(ivs.Contains(Convert.ToHexString(AesGcmPdf.IvFor(number)))).IsEqualTo(!rewritten);
        }
    }

    /// <summary>Gets the object number of the page content stream in a saved file, which a compact save may renumber.</summary>
    /// <param name="store">The saved file's objects.</param>
    /// <returns>The object number.</returns>
    private static int ContentNumberAfter(PdfObjectStore store)
    {
        var page = store.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!.GetDictionary(0)!;
        return page.GetRaw(KnownName.Contents).AsReference().Number;
    }

    /// <summary>Lists the IVs at the start of every encrypted stream in a saved file.</summary>
    /// <param name="store">The saved file's objects.</param>
    /// <returns>The IVs as hexadecimal text.</returns>
    private static List<string> SavedStreamIvs(PdfObjectStore store)
    {
        var ivs = new List<string>();
        for (var number = 1; number < store.Size; number++)
        {
            if (StoreReading.GetObject(store, new(number, 0)).AsStream() is not { IsEncrypted: true } stream)
            {
                continue;
            }

            using var raw = stream.LeaseRawData();
            if (raw.Span.Length >= AesGcmCipher.Overhead)
            {
                ivs.Add(Convert.ToHexString(raw.Span[..AesGcmPdf.IvLength]));
            }
        }

        return ivs;
    }

    /// <summary>Reads and decodes a stream.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="number">The stream's object number.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] ReadStream(PdfObjectStore store, int number) =>
        StoreReading.GetObject(store, new(number, 0)).AsStream()!.DecodeToArray();

    /// <summary>Reads the document title.</summary>
    /// <param name="store">The objects.</param>
    /// <returns>The title.</returns>
    private static string? ReadTitle(PdfObjectStore store) => store.Trailer.GetDictionary(KnownName.Info)!.GetText(KnownName.Title);

    /// <summary>Creates a handler for the builder's file.</summary>
    /// <returns>The handler.</returns>
    private static PdfSecurityHandler Handler() => AesGcmPdf.CreateHandler(AesGcmPdf.Create(AesGcmLayout.Everything, true));
}
