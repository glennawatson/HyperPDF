// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Tests.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests that the writers encrypt metadata and named crypt filter streams the way the reader expects.</summary>
public sealed class EncryptedWriterTests
{
    /// <summary>The object number of an ordinary stream.</summary>
    private const int Ordinary = 3;

    /// <summary>The object number of a metadata stream.</summary>
    private const int Metadata = 4;

    /// <summary>The object number of a stream with an Identity crypt filter.</summary>
    private const int Identity = 5;

    /// <summary>The object number of a stream with a crypt filter named Other.</summary>
    private const int Named = 6;

    /// <summary>The /Size of the plain sample.</summary>
    private const int PlainSize = 7;

    /// <summary>The catalog object, which lists the streams so they stay reachable when a writer drops unused objects.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R /Fields [3 0 R 4 0 R 5 0 R 6 0 R] >>";

    /// <summary>The page tree object.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The streams, by object number, in the order they are checked.</summary>
    private static readonly int[] StreamNumbers = [Ordinary, Metadata, Identity, Named];

    /// <summary>Gets the text of the ordinary stream.</summary>
    private static ReadOnlySpan<byte> OrdinaryText => "ordinary stream text"u8;

    /// <summary>Gets the text of the metadata stream.</summary>
    private static ReadOnlySpan<byte> MetadataText => "<x:xmpmeta>metadata</x:xmpmeta>"u8;

    /// <summary>Gets the text of the Identity stream.</summary>
    private static ReadOnlySpan<byte> IdentityText => "identity stream text"u8;

    /// <summary>Gets the text of the named stream.</summary>
    private static ReadOnlySpan<byte> NamedText => "named filter stream text"u8;

    /// <summary>The object writer encrypts what the reader decrypts, and leaves clear what the reader leaves clear.</summary>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ObjectWriterRoundTripsStreams(bool encryptMetadata)
    {
        var encrypted = WriteEncrypted(PlainFile(), encryptMetadata);
        await Assert.That(Problems(encrypted, encryptMetadata)).IsEqualTo(string.Empty);
    }

    /// <summary>Saving an opened encrypted file again keeps every stream readable and in the same encrypted or clear state.</summary>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompactWriterKeepsStreamsReadable(bool encryptMetadata)
    {
        var encrypted = WriteEncrypted(PlainFile(), encryptMetadata);
        byte[] saved;
        using (var store = StoreOpening.Open(encrypted, null))
        {
            saved = PdfCompactWriter.Save(store, new(false, false));
        }

        await Assert.That(Problems(saved, encryptMetadata)).IsEqualTo(string.Empty);
    }

    /// <summary>Saving an opened encrypted file with an incremental update keeps every stream readable.</summary>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IncrementalWriterKeepsStreamsReadable(bool encryptMetadata)
    {
        var encrypted = WriteEncrypted(PlainFile(), encryptMetadata);
        byte[] saved;
        using (var store = StoreOpening.Open(encrypted, null))
        {
            foreach (var number in StreamNumbers)
            {
                StoreEditing.Replace(store, new(number, 0), StoreReading.GetObject(store, new(number, 0)));
            }

            saved = PdfIncrementalWriter.Save(store);
        }

        await Assert.That(Problems(saved, encryptMetadata)).IsEqualTo(string.Empty);
    }

    /// <summary>Describes how the streams of a file differ from what was written.</summary>
    /// <param name="file">The encrypted file.</param>
    /// <param name="encryptMetadata">Whether metadata is expected to be encrypted.</param>
    /// <returns>An empty string when everything matches.</returns>
    private static string Problems(byte[] file, bool encryptMetadata)
    {
        using var store = StoreOpening.Open(file, null);
        var problems = new StringBuilder();
        var streams = store.Catalog.GetArray(KnownName.Fields)!;
        foreach (var number in StreamNumbers)
        {
            // Writers that renumber objects keep the catalog's array in order, so each stream is found by its position.
            var stream = streams.Get(number - Ordinary).AsStream()!;
            var plain = TextOf(number);
            if (!stream.DecodeToArray().AsSpan().SequenceEqual(plain))
            {
                _ = problems.Append($"stream {number} decodes wrongly; ");
            }

            var expectClear = number == Identity || (number == Metadata && !encryptMetadata);
            if (stream.CopyRawData().AsSpan().SequenceEqual(plain) != expectClear)
            {
                _ = problems.Append($"stream {number} is stored {(expectClear ? "encrypted" : "in the clear")}; ");
            }
        }

        return problems.ToString();
    }

    /// <summary>Gets the plain text of a stream.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The text.</returns>
    private static byte[] TextOf(int number) => number switch
    {
        Ordinary => OrdinaryText.ToArray(),
        Metadata => MetadataText.ToArray(),
        Identity => IdentityText.ToArray(),
        _ => NamedText.ToArray(),
    };

    /// <summary>Builds a plain file with the four kinds of stream.</summary>
    /// <returns>The file.</returns>
    private static byte[] PlainFile() =>
        new RawPdf().Object(
        1,
        Catalog).Object(
        PagesNumber,
        Pages).Stream(
        Ordinary,
        string.Empty,
        OrdinaryText).Stream(
        Metadata,
        "/Type /Metadata /Subtype /XML",
        MetadataText).Stream(
        Identity,
        "/Filter /Crypt /DecodeParms << /Name /Identity >>",
        IdentityText).Stream(
        Named,
        "/Filter /Crypt /DecodeParms << /Name /Other >>",
        NamedText).Table(
        PlainSize,
        "/Root 1 0 R").ToArray();

    /// <summary>Writes every object of a plain file encrypted with the object writer, then a table and trailer.</summary>
    /// <param name="plainFile">The plain file.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The encrypted file.</returns>
    private static byte[] WriteEncrypted(byte[] plainFile, bool encryptMetadata)
    {
        using var plain = StoreOpening.Open(plainFile, null);
        using var setup = EncryptionSetup.Revision4(encryptMetadata, plain.Names);
        var writer = new PdfObjectWriter(plain.Names, setup.Handler);
        try
        {
            writer.WriteRaw("%PDF-1.7\n"u8);
            var rows = new List<XrefRow> { XrefRow.FreeHead, };
            for (var number = 1; number < plain.Size; number++)
            {
                rows.Add(new(number, XrefEntryType.InFile, writer.Length, 0));
                writer.WriteIndirectObject(new(number, 0), StoreReading.GetObject(plain, new(number, 0)));
            }

            var encryptNumber = plain.Size;
            rows.Add(new(encryptNumber, XrefEntryType.InFile, writer.Length, 0));
            writer.WriteIndirectObject(new(encryptNumber, 0), PdfValue.FromDictionary(setup.EncryptDictionary), false);
            var trailer = PdfXrefWriter.CreateTrailer(plain.Trailer, encryptNumber + 1, -1, false);
            trailer.Set(KnownName.Encrypt, PdfValue.FromReference(new(encryptNumber, 0)));
            trailer.Set(KnownName.ID, PdfValue.FromArray(new(null, [PdfValue.FromString(setup.FileId), PdfValue.FromString(setup.FileId)])));
            var offset = writer.Length;
            PdfXrefWriter.WriteTable(ref writer, rows.ToArray());
            PdfXrefWriter.WriteTrailer(ref writer, trailer, offset);
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }
}
