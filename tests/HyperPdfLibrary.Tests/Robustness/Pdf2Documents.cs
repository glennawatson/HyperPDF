// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Builds PDF 2.0 documents: UTF-8 text strings, output intents, and AES-256 (revision 6) encryption.</summary>
internal static class Pdf2Documents
{
    /// <summary>The length of the file key and of the hashes.</summary>
    private const int KeyLength = 32;

    /// <summary>The length of a validation or key salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The length of the first 48 bytes of /U that the owner hash covers.</summary>
    private const int UserDataLength = 48;

    /// <summary>The length of the /Perms value.</summary>
    private const int PermsLength = 16;

    /// <summary>The length of the first file id.</summary>
    private const int IdLength = 16;

    /// <summary>The number of rounds the revision 6 hash always runs.</summary>
    private const int MinRounds = 64;

    /// <summary>The offset subtracted from the round count in the hash's exit test.</summary>
    private const int ExitOffset = 32;

    /// <summary>The number of hash functions the revision 6 hash picks from.</summary>
    private const int HashChoices = 3;

    /// <summary>The permissions written: everything allowed.</summary>
    private const int Permissions = -4;

    /// <summary>The multiplier that makes the deterministic bytes differ from one another.</summary>
    private const int ByteStep = 37;

    /// <summary>The seed of the file key.</summary>
    private const int FileKeySeed = 2;

    /// <summary>The seed of the user validation salt.</summary>
    private const int UserValidationSeed = 3;

    /// <summary>The seed of the user key salt.</summary>
    private const int UserKeySeed = 4;

    /// <summary>The seed of the owner validation salt.</summary>
    private const int OwnerValidationSeed = 5;

    /// <summary>The seed of the owner key salt.</summary>
    private const int OwnerKeySeed = 6;

    /// <summary>The offset of the "adb" marker in /Perms.</summary>
    private const int PermsMarkerOffset = 9;

    /// <summary>The offset of the encrypt-metadata flag in /Perms.</summary>
    private const int PermsMetadataOffset = 8;

    /// <summary>The offset of the unsigned all-ones block in /Perms.</summary>
    private const int PermsOnesOffset = 4;

    /// <summary>The page object of the PDF 2.0 text document.</summary>
    private const string TextPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << >> /Contents 5 0 R /OutputIntents [8 0 R] >>";

    /// <summary>The UTF-8 text with a byte order mark: "Hello" and a euro sign.</summary>
    private const string Utf8Hello = "<EFBBBF48656C6C6FE282AC>";

    /// <summary>The UTF-8 text with a byte order mark: "Jörn".</summary>
    private const string Utf8Author = "<EFBBBF4AC3B6726E>";

    /// <summary>The page content of the PDF 2.0 text document.</summary>
    private const string TextContent = "0 0 m 10 10 l S";

    /// <summary>Gets the "Hello" and euro text the PDF 2.0 documents carry.</summary>
    internal static string HelloText => "Hello€";

    /// <summary>Gets the page content of the PDF 2.0 text document.</summary>
    internal static string ContentText => TextContent;

    /// <summary>Builds a small PDF 2.0 file with UTF-8 strings, output intents at page and catalog level, and a /Version.</summary>
    /// <returns>The file.</returns>
    internal static byte[] CreateText()
    {
        var file = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Version /2.0 /Outlines 6 0 R /OutputIntents [8 0 R] >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        TextPage,
        "<< /Type /Page /Parent 2 0 R >>",
        MiniPdf.Stream(
        string.Empty,
        TextContent),
        "<< /Type /Outlines /First 10 0 R /Last 10 0 R /Count 1 >>",
        $"<< /Title {Utf8Hello} /Author {Utf8Author} >>",
        "<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (CGATS TR 001) /DestOutputProfile 9 0 R >>",
        MiniPdf.Stream(
        "/N 3",
        "this is not an ICC profile"),
        $"<< /Title {Utf8Hello} /Parent 6 0 R /Dest [3 0 R /Fit] >>");
        return Replace(Replace(file, "%PDF-1.7", "%PDF-2.0"), "/Root 1 0 R", "/Root 1 0 R /Info 7 0 R");
    }

    /// <summary>Replaces text in a file of one byte per character.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The text to find.</param>
    /// <param name="to">The replacement.</param>
    /// <returns>The new file.</returns>
    internal static byte[] Replace(byte[] file, string from, string to) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(file).Replace(from, to, StringComparison.Ordinal));

    /// <summary>
    /// Re-encrypts a plain document with AES-256 (revision 6) using the given passwords, writing a PDF 2.0 header. The
    /// document is written the same way a PDF 2.0 writer would: every string and stream encrypted with the file key.
    /// </summary>
    /// <param name="plainFile">The plain document.</param>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password.</param>
    /// <returns>The encrypted document.</returns>
    internal static byte[] EncryptAes256(byte[] plainFile, string userPassword, string ownerPassword)
    {
        using var plain = StoreOpening.Open(plainFile, null);
        var firstId = Bytes(IdLength, 1);
        var encrypt = CreateEncryptDictionary(plain.Names, userPassword, ownerPassword);
        _ = PdfSecurityHandler.TryCreate(encrypt, firstId, userPassword, out var handler);
        using (handler)
        {
            return WriteEncrypted(plain, handler!, encrypt, firstId);
        }
    }

    /// <summary>Builds the /Encrypt dictionary of a revision 6 file.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary CreateEncryptDictionary(PdfNameTable names, string userPassword, string ownerPassword)
    {
        var text = EncryptText(userPassword, ownerPassword, "/CFM /AESV3");
        var parser = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, names);
        return parser.ParseValue().AsDictionary()!;
    }

    /// <summary>Builds the text of a revision 6 /Encrypt dictionary whose crypt filter has the given method entry.</summary>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password.</param>
    /// <param name="method">The crypt filter method entry, such as <c>/CFM /AESV3</c>.</param>
    /// <returns>The dictionary text.</returns>
    internal static string EncryptText(string userPassword, string ownerPassword, string method)
    {
        var fileKey = Bytes(KeyLength, FileKeySeed);
        var userValidation = Bytes(SaltLength, UserValidationSeed);
        var userKeySalt = Bytes(SaltLength, UserKeySeed);
        var ownerValidation = Bytes(SaltLength, OwnerValidationSeed);
        var ownerKeySalt = Bytes(SaltLength, OwnerKeySeed);
        var user = Concat(Hash(userPassword, userValidation, []), userValidation, userKeySalt);
        var userKey = WrapKey(Hash(userPassword, userKeySalt, []), fileKey);
        var owner = Concat(Hash(ownerPassword, ownerValidation, user), ownerValidation, ownerKeySalt);
        var ownerKey = WrapKey(Hash(ownerPassword, ownerKeySalt, user), fileKey);
        var perms = CreatePerms(fileKey);
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"<< /Filter /Standard /V 5 /R 6 /Length 256 /CF << /StdCF << {method} /AuthEvent /DocOpen /Length 32 >> >> ");
        _ = text.Append(CultureInfo.InvariantCulture, $"/StmF /StdCF /StrF /StdCF /P {Permissions} /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> ");
        _ = text.Append(CultureInfo.InvariantCulture, $"/OE <{Convert.ToHexString(ownerKey)}> /UE <{Convert.ToHexString(userKey)}> /Perms <{Convert.ToHexString(perms)}> >>");
        return text.ToString();
    }

    /// <summary>Gets the first file id the encrypted documents use.</summary>
    /// <returns>The id.</returns>
    internal static byte[] FirstId() => Bytes(IdLength, 1);

    /// <summary>Makes bytes that differ from one another and from other seeds.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = unchecked((byte)((seed * ByteStep) + (i * ByteStep) + i));
        }

        return bytes;
    }

    /// <summary>Joins byte arrays.</summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Concat(params byte[][] parts)
    {
        var length = 0;
        foreach (var part in parts)
        {
            length += part.Length;
        }

        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    /// <summary>Wraps the file key with AES-256-CBC, a zero IV and no padding (/UE and /OE).</summary>
    /// <param name="intermediate">The intermediate key.</param>
    /// <param name="fileKey">The file key.</param>
    /// <returns>The wrapped key.</returns>
    private static byte[] WrapKey(byte[] intermediate, byte[] fileKey)
    {
        using var aes = Aes.Create();
        aes.Key = intermediate;
        return aes.EncryptCbc(fileKey, new byte[IdLength], PaddingMode.None);
    }

    /// <summary>Creates /Perms: the permissions, an all-ones block, the metadata flag and "adb", encrypted with AES-256-ECB.</summary>
    /// <param name="fileKey">The file key.</param>
    /// <returns>The value.</returns>
    private static byte[] CreatePerms(byte[] fileKey)
    {
        var block = new byte[PermsLength];
        BinaryPrimitives.WriteInt32LittleEndian(block, Permissions);
        block.AsSpan(PermsOnesOffset, PermsOnesOffset).Fill(byte.MaxValue);
        block[PermsMetadataOffset] = (byte)'T';
        "adb"u8.CopyTo(block.AsSpan(PermsMarkerOffset));
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    /// <summary>Computes the revision 6 password hash (ISO 32000-2 algorithm 2.B).</summary>
    /// <param name="password">The password.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="userData">The first 48 bytes of /U for owner hashes, or empty.</param>
    /// <returns>The 32-byte hash.</returns>
    private static byte[] Hash(string password, byte[] salt, byte[] userData)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var k = SHA256.HashData(Concat(passwordBytes, salt, userData[..Math.Min(userData.Length, UserDataLength)]));
        var done = 0;
        while (true)
        {
            var e = Round(passwordBytes, k, userData);
            k = NextHash(e);
            done++;
            if (done >= MinRounds && e[^1] <= done - ExitOffset)
            {
                return k[..KeyLength];
            }
        }
    }

    /// <summary>Runs one round of the revision 6 hash.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="k">The hash so far.</param>
    /// <param name="userData">The user data.</param>
    /// <returns>The encrypted block sequence.</returns>
    private static byte[] Round(byte[] password, byte[] k, byte[] userData)
    {
        var unit = Concat(password, k, userData[..Math.Min(userData.Length, UserDataLength)]);
        var repeated = new byte[unit.Length * MinRounds];
        for (var i = 0; i < MinRounds; i++)
        {
            unit.CopyTo(repeated, i * unit.Length);
        }

        using var aes = Aes.Create();
        aes.Key = k[..IdLength];
        return aes.EncryptCbc(repeated, k[IdLength..KeyLength], PaddingMode.None);
    }

    /// <summary>Hashes a round's output with SHA-256, SHA-384 or SHA-512, chosen by its first 16 bytes read as a number mod 3.</summary>
    /// <param name="e">The round's output.</param>
    /// <returns>The hash.</returns>
    private static byte[] NextHash(byte[] e)
    {
        var sum = 0;
        for (var i = 0; i < IdLength; i++)
        {
            sum += e[i];
        }

        return (sum % HashChoices) switch
        {
            0 => SHA256.HashData(e),
            1 => SHA384.HashData(e),
            _ => SHA512.HashData(e),
        };
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
            writer.WriteRaw("%PDF-2.0\n"u8);
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
