// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Builds and inspects documents for the writer tests.</summary>
internal static class WritingTestDocuments
{
    /// <summary>The length of the padded password and of /O and /U.</summary>
    private const int PaddedLength = 32;

    /// <summary>The length of the first file id.</summary>
    private const int IdLength = 16;

    /// <summary>The key length of revision 2 encryption.</summary>
    private const int Revision2KeyLength = 5;

    /// <summary>The permissions written: everything allowed.</summary>
    private const int Permissions = -4;

    /// <summary>The deepest page tree followed.</summary>
    private const int MaxDepth = 16;

    /// <summary>Gets the padding mixed into passwords before revision 5.</summary>
    private static ReadOnlySpan<byte> Padding =>
        [0x28,
        0xBF,
        0x4E,
        0x5E,
        0x4E,
        0x75,
        0x8A,
        0x41,
        0x64,
        0x00,
        0x4E,
        0x56,
        0xFF,
        0xFA,
        0x01,
        0x08,
        0x2E,
        0x2E,
        0x00,
        0xB6,
        0xD0,
        0x68,
        0x3E,
        0x80,
        0x2F,
        0x0C,
        0xA9,
        0xFE,
        0x64,
        0x53,
        0x69,
        0x7A,
        ];

    /// <summary>Gets the decoded content stream of every page, in page order.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The content bytes of each page.</returns>
    internal static List<byte[]> PageContents(PdfObjectStore store)
    {
        var contents = new List<byte[]>();
        CollectPages(store.Catalog.GetDictionary(KnownName.Pages), contents, 0);
        return contents;
    }

    /// <summary>Counts the objects from 1 to /Size - 1 that do not resolve.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The number of missing objects.</returns>
    internal static int CountMissing(PdfObjectStore store)
    {
        var missing = 0;
        for (var number = 1; number < store.Size; number++)
        {
            missing += StoreReading.GetObject(store, new(number, 0)).IsNull ? 1 : 0;
        }

        return missing;
    }

    /// <summary>
    /// Re-encrypts a plain document with RC4 40-bit (revision 2) and an empty user password, using the object writer's
    /// encryption, so the writers can be tested on encrypted input.
    /// </summary>
    /// <param name="plainFile">The plain document.</param>
    /// <returns>The encrypted document.</returns>
    internal static byte[] Encrypt(byte[] plainFile)
    {
        using var plain = StoreOpening.Open(plainFile, null);
        var firstId = new byte[IdLength];
        var owner = new byte[PaddedLength];
        for (var i = 0; i < owner.Length; i++)
        {
            owner[i] = (byte)(i + 1);
            firstId[i % IdLength] ^= (byte)(i * Revision2KeyLength);
        }

        var encrypt = CreateEncryptDictionary(plain.Names, owner, firstId);
        _ = PdfSecurityHandler.TryCreate(encrypt, firstId, null, out var handler);
        using (handler)
        {
            return WriteEncrypted(plain, handler!, encrypt, firstId);
        }
    }

    /// <summary>Walks the page tree.</summary>
    /// <param name="node">The page tree node.</param>
    /// <param name="contents">The page contents found.</param>
    /// <param name="depth">The depth.</param>
    private static void CollectPages(PdfDictionary? node, List<byte[]> contents, int depth)
    {
        if (node is null || depth > MaxDepth)
        {
            return;
        }

        if (node.IsName(KnownName.Type, KnownName.Page))
        {
            contents.Add(node.GetStream(KnownName.Contents)?.DecodeToArray() ?? []);
            return;
        }

        var kids = node.GetArray(KnownName.Kids);
        for (var i = 0; i < (kids?.Count ?? 0); i++)
        {
            CollectPages(kids!.GetDictionary(i), contents, depth + 1);
        }
    }

    /// <summary>Creates a revision 2 /Encrypt dictionary whose user password is empty.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="owner">The /O value.</param>
    /// <param name="firstId">The first file id.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary CreateEncryptDictionary(PdfNameTable names, byte[] owner, byte[] firstId)
    {
        var input = new byte[PaddedLength + PaddedLength + sizeof(int) + IdLength];
        Padding.CopyTo(input);
        owner.CopyTo(input, PaddedLength);
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(PaddedLength + PaddedLength), Permissions);
        firstId.CopyTo(input, PaddedLength + PaddedLength + sizeof(int));
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(input, digest);
        var user = Padding.ToArray();
        Rc4.Apply(digest.AsSpan(0, Revision2KeyLength), user);
        var text = $"<< /Filter /Standard /V 1 /R 2 /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /P {Permissions} >>";
        var parser = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, names);
        return parser.ParseValue().AsDictionary()!;
    }

    /// <summary>Writes every object of a plain document encrypted, then the /Encrypt dictionary, table and trailer.</summary>
    /// <param name="plain">The plain document.</param>
    /// <param name="handler">The security handler.</param>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="firstId">The first file id.</param>
    /// <returns>The encrypted file.</returns>
    private static byte[] WriteEncrypted(PdfObjectStore plain, PdfSecurityHandler handler, PdfDictionary encrypt, byte[] firstId)
    {
        var writer = new PdfObjectWriter(plain.Names, handler);
        try
        {
            writer.WriteRaw("%PDF-1.4\n"u8);
            var rows = new List<XrefRow> { XrefRow.FreeHead, };
            for (var number = 1; number < plain.Size; number++)
            {
                rows.Add(new(number, XrefEntryType.InFile, writer.Length, 0));
                writer.WriteIndirectObject(new(number, 0), StoreReading.GetObject(plain, new(number, 0)));
            }

            var encryptNumber = plain.Size;
            rows.Add(new(encryptNumber, XrefEntryType.InFile, writer.Length, 0));
            writer.WriteIndirectObject(new(encryptNumber, 0), PdfValue.FromDictionary(encrypt), false);
            var trailer = PdfXrefWriter.CreateTrailer(plain.Trailer, encryptNumber + 1, -1, false);
            trailer.Set(KnownName.Encrypt, PdfValue.FromReference(new(encryptNumber, 0)));
            trailer.Set(KnownName.ID, PdfValue.FromArray(new(null, [PdfValue.FromString(firstId), PdfValue.FromString(firstId)])));
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
