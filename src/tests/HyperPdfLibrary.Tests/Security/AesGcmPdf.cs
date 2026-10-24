// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Tests.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Builds a small PDF 2.0 file protected as ISO/TS 32003:2023 describes (version 6, revision 7, crypt filter AESV4), working
/// from the standard and .NET's <see cref="AesGcm"/> with no code from the library: the password hash (ISO 32000-2 7.6.4.3.4),
/// the keys, /Perms and the serialized IV, cipher text and tag of each string and stream.
/// </summary>
internal static class AesGcmPdf
{
    /// <summary>The user password.</summary>
    internal const string UserPassword = "user-pw";

    /// <summary>The owner password.</summary>
    internal const string OwnerPassword = "owner-pw";

    /// <summary>The object number of the page content stream.</summary>
    internal const int ContentNumber = 4;

    /// <summary>The object number of the document information dictionary.</summary>
    internal const int InfoNumber = 5;

    /// <summary>The object number of the /Encrypt dictionary.</summary>
    internal const int EncryptNumber = 6;

    /// <summary>The object number of the embedded file stream.</summary>
    internal const int AttachmentNumber = 7;

    /// <summary>The /Size of the file.</summary>
    internal const int Size = 8;

    /// <summary>The length of an AES-256 key and of a SHA-256 hash.</summary>
    internal const int KeyLength = 32;

    /// <summary>The length of an AES-GCM IV.</summary>
    internal const int IvLength = 12;

    /// <summary>The length of an AES-GCM tag.</summary>
    internal const int TagLength = 16;

    /// <summary>The object number of the page tree root.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the page.</summary>
    private const int PageNumber = 3;

    /// <summary>The length of a password salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The length of an AES block.</summary>
    private const int BlockLength = 16;

    /// <summary>The permissions: everything allowed.</summary>
    private const int Permissions = -4;

    /// <summary>The length of the trailer file identifier.</summary>
    private const int IdLength = 16;

    /// <summary>The times the round input is repeated (algorithm 2.B step a).</summary>
    private const int RoundRepeats = 64;

    /// <summary>The fewest rounds of the hash (algorithm 2.B).</summary>
    private const int MinimumRounds = 64;

    /// <summary>The margin the last byte is compared with (algorithm 2.B).</summary>
    private const int RoundMargin = 32;

    /// <summary>The number of SHA-2 variants the round chooses between.</summary>
    private const int HashChoices = 3;

    /// <summary>The place of the marker in the decrypted /Perms block.</summary>
    private const int PermsMarker = 8;

    /// <summary>The seed of the file key.</summary>
    private const byte KeySeed = 0x11;

    /// <summary>The seed of the IV of each object.</summary>
    private const byte IvSeed = 0xA0;

    /// <summary>The seed of the file identifier.</summary>
    private const byte IdSeed = 0x61;

    /// <summary>The ISO_ developer extension of Table 1 in ISO/TS 32003.</summary>
    private const string ExtensionStart = "/Extensions << /ISO_ [<< /Type /DeveloperExtensions /BaseVersion /2.0 /ExtensionLevel 32003";

    /// <summary>The number the IV of the extension's revision string is taken from; the catalog holds two strings and each needs its own IV.</summary>
    private const int RevisionIvNumber = 20;

    /// <summary>The number the IV of the extension's URL string is taken from.</summary>
    private const int UrlIvNumber = 21;

    /// <summary>Gets the page content.</summary>
    internal static string ContentText => "0 0 1 rg 20 20 100 100 re f";

    /// <summary>Gets the document title.</summary>
    internal static string TitleText => "Quarterly report";

    /// <summary>Gets the embedded file's text.</summary>
    internal static string AttachmentText => "attachment text that only AES-GCM protects";

    /// <summary>Gets the file key every crypt filter uses.</summary>
    internal static byte[] FileKey => Sequence(KeyLength, KeySeed);

    /// <summary>Builds the file.</summary>
    /// <param name="layout">Which crypt filters protect which data.</param>
    /// <param name="declareExtension">Whether the catalog declares the ISO_ extension.</param>
    /// <returns>The file, with a PDF 2.0 header.</returns>
    internal static byte[] Create(AesGcmLayout layout, bool declareExtension) =>
        Create(layout, declareExtension, Protect(AttachmentNumber, Bytes(AttachmentText)));

    /// <summary>Builds the file with chosen bytes stored in the embedded file stream.</summary>
    /// <param name="layout">Which crypt filters protect which data.</param>
    /// <param name="declareExtension">Whether the catalog declares the ISO_ extension.</param>
    /// <param name="attachment">The bytes stored as the embedded file stream's data.</param>
    /// <returns>The file, with a PDF 2.0 header.</returns>
    internal static byte[] Create(AesGcmLayout layout, bool declareExtension, byte[] attachment)
    {
        var everything = layout == AesGcmLayout.Everything;
        var catalog = declareExtension ? $"<< /Type /Catalog /Pages 2 0 R {Extension(everything)} >>" : "<< /Type /Catalog /Pages 2 0 R >>";
        var content = Bytes(ContentText);
        var title = Bytes(TitleText);
        var info = $"<< /Title <{Convert.ToHexString(everything ? Protect(InfoNumber, title) : title)}> >>";
        var id = Convert.ToHexString(Sequence(IdLength, IdSeed));
        var file = new RawPdf()
            .Object(1, catalog)
            .Object(PagesNumber, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
            .Object(PageNumber, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>")
            .Stream(ContentNumber, string.Empty, everything ? Protect(ContentNumber, content) : content)
            .Object(InfoNumber, info)
            .Object(EncryptNumber, EncryptDictionary(layout, UserPassword, OwnerPassword))
            .Stream(AttachmentNumber, "/Type /EmbeddedFile", attachment)
            .Table(Size, $"/Root 1 0 R /Info {InfoNumber} 0 R /Encrypt {EncryptNumber} 0 R /ID [<{id}> <{id}>]")
            .ToArray();

        // The header is "%PDF-1.7"; only the last two bytes change, so no offset moves.
        "2.0"u8.CopyTo(file.AsSpan("%PDF-"u8.Length));
        return file;
    }

    /// <summary>Reads the /Encrypt dictionary text out of a file made by the builder.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The dictionary text.</returns>
    internal static string EncryptText(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);
        var header = $"{EncryptNumber} 0 obj\n";
        var start = text.IndexOf(header, StringComparison.Ordinal) + header.Length;
        return text[start..text.IndexOf("\nendobj", start, StringComparison.Ordinal)];
    }

    /// <summary>Creates the library's handler for a file made by the builder, opened with the user password.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The handler, which the caller disposes.</returns>
    internal static PdfSecurityHandler CreateHandler(byte[] file)
    {
        var dictionary = new PdfParser(Encoding.ASCII.GetBytes(EncryptText(file)), 0, null, new()).ParseValue().AsDictionary()!;
        _ = PdfSecurityHandler.TryCreate(dictionary, [], UserPassword, out var handler);
        return handler!;
    }

    /// <summary>Writes the /Encrypt dictionary of a standard security handler that protects every string and stream with AES-256.</summary>
    /// <param name="gcm">Whether to use AES-GCM (/V 6, /R 7, ISO/TS 32003) instead of AES-256 in CBC mode (/V 5, /R 6).</param>
    /// <param name="permissions">The permission flags, in /P and in the encrypted /Perms.</param>
    /// <param name="extra">Extra entries, such as /KDFSalt.</param>
    /// <returns>The dictionary as PDF text.</returns>
    internal static string StandardEncryptDictionary(bool gcm, int permissions, string extra)
    {
        var fileKey = FileKey;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Filter /Standard {(gcm ? "/V 6 /R 7" : "/V 5 /R 6")} /Length 256 "
            + $"/CF << /StdCF << /AuthEvent /DocOpen /CFM /{(gcm ? "AESV4" : "AESV3")} /Length 32 >> >> /StmF /StdCF /StrF /StdCF "
            + $"{PasswordEntries(fileKey, UserPassword, OwnerPassword)} /Perms <{Convert.ToHexString(Perms(fileKey, permissions))}> /P {permissions} {extra} >>");
    }

    /// <summary>Gets the IV used for an object, which the test chooses so each object has its own.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The 12-byte IV.</returns>
    internal static byte[] IvFor(int number) => Sequence(IvLength, (byte)(IvSeed + (number * IvLength)));

    /// <summary>Serializes data as ISO/TS 32003 5.2 describes: the IV, the cipher text, then the tag, with no additional data.</summary>
    /// <param name="number">The object number, which selects the IV.</param>
    /// <param name="plain">The plain bytes.</param>
    /// <returns>The protected bytes.</returns>
    internal static byte[] Protect(int number, byte[] plain)
    {
        var iv = IvFor(number);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagLength];
        using var gcm = new AesGcm(FileKey, TagLength);
        gcm.Encrypt(iv, plain, cipher, tag);
        return [.. iv, .. cipher, .. tag];
    }

    /// <summary>Finds the first place a sequence of bytes occurs in a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="value">The bytes.</param>
    /// <returns>The offset.</returns>
    /// <exception cref="InvalidOperationException">The bytes are not in the file.</exception>
    internal static int Find(byte[] file, byte[] value)
    {
        var at = file.AsSpan().IndexOf(value);
        return at >= 0 ? at : throw new InvalidOperationException("The bytes are not in the file.");
    }

    /// <summary>Writes the /Extensions entry of Table 1; its two strings are encrypted like any string of an encrypted document.</summary>
    /// <param name="encryptStrings">Whether the default crypt filter protects strings.</param>
    /// <returns>The entry as PDF text.</returns>
    private static string Extension(bool encryptStrings)
    {
        var revision = ":2023"u8.ToArray();
        var url = "https://www.iso.org/standard/45876.html"u8.ToArray();
        var revisionText = encryptStrings ? $"<{Convert.ToHexString(Protect(RevisionIvNumber, revision))}>" : "(:2023)";
        var urlText = encryptStrings ? $"<{Convert.ToHexString(Protect(UrlIvNumber, url))}>" : "(https://www.iso.org/standard/45876.html)";
        return $"{ExtensionStart} /ExtensionRevision {revisionText} /URL {urlText} >>] >>";
    }

    /// <summary>Writes the /Encrypt dictionary for an empty-of-secrets file: keys for both passwords and the crypt filters.</summary>
    /// <param name="layout">Which crypt filters protect which data.</param>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password.</param>
    /// <returns>The dictionary as PDF text.</returns>
    private static string EncryptDictionary(AesGcmLayout layout, string userPassword, string ownerPassword)
    {
        var fileKey = FileKey;
        var filters = layout == AesGcmLayout.Everything
            ? "/CF << /StdCF << /AuthEvent /DocOpen /CFM /AESV4 /Length 32 >> >> /StmF /StdCF /StrF /StdCF"
            : "/CF << /EmbedCF << /AuthEvent /EFOpen /CFM /AESV4 /Length 32 >> >> /StmF /Identity /StrF /Identity /EFF /EmbedCF";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Filter /Standard /V 6 /R 7 /Length 256 {filters} {PasswordEntries(fileKey, userPassword, ownerPassword)} "
            + $"/Perms <{Convert.ToHexString(Perms(fileKey, Permissions))}> /P {Permissions} >>");
    }

    /// <summary>Writes the /O, /U, /OE and /UE entries for both passwords.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password.</param>
    /// <returns>The entries as PDF text.</returns>
    private static string PasswordEntries(byte[] fileKey, string userPassword, string ownerPassword)
    {
        var user = PasswordKey(Encoding.UTF8.GetBytes(userPassword), Sequence(SaltLength, 0x21), Sequence(SaltLength, 0x31), [], fileKey, out var userKeyWrap);
        var owner = PasswordKey(Encoding.UTF8.GetBytes(ownerPassword), Sequence(SaltLength, 0x41), Sequence(SaltLength, 0x51), user, fileKey, out var ownerKeyWrap);
        return $"/O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /OE <{Convert.ToHexString(ownerKeyWrap)}> /UE <{Convert.ToHexString(userKeyWrap)}>";
    }

    /// <summary>Works out /U or /O and its encrypted copy of the file key (algorithms 8 and 9).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="validationSalt">The validation salt.</param>
    /// <param name="keySalt">The key salt.</param>
    /// <param name="userKey">The 48-byte /U value for the owner password; empty for the user password.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="wrapped">The /UE or /OE value.</param>
    /// <returns>The 48-byte /U or /O value.</returns>
    private static byte[] PasswordKey(byte[] password, byte[] validationSalt, byte[] keySalt, byte[] userKey, byte[] fileKey, out byte[] wrapped)
    {
        using var aes = Aes.Create();
        aes.Key = Hash(password, keySalt, userKey);
        wrapped = aes.EncryptCbc(fileKey, new byte[BlockLength], PaddingMode.None);
        return [.. Hash(password, validationSalt, userKey), .. validationSalt, .. keySalt];
    }

    /// <summary>Encrypts a permissions block holding chosen flags with the file key (algorithm 10).</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="permissions">The permission flags.</param>
    /// <returns>The 16-byte /Perms value.</returns>
    private static byte[] Perms(byte[] fileKey, int permissions)
    {
        var block = new byte[BlockLength];
        BinaryPrimitives.WriteInt32LittleEndian(block, permissions);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(sizeof(int)), -1);
        "Tadb"u8.CopyTo(block.AsSpan(PermsMarker));
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    /// <summary>The password hash of revisions 6 and 7 (algorithm 2.B).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="userKey">The 48-byte /U value for owner hashes; empty for user hashes.</param>
    /// <returns>The 32-byte hash.</returns>
    private static byte[] Hash(byte[] password, byte[] salt, byte[] userKey)
    {
        var key = SHA256.HashData([.. password, .. salt, .. userKey]);
        using var aes = Aes.Create();
        var round = 0;
        var last = 0;
        while (round < MinimumRounds || round < last + RoundMargin)
        {
            byte[] once = [.. password, .. key, .. userKey];
            var repeated = new byte[once.Length * RoundRepeats];
            for (var i = 0; i < RoundRepeats; i++)
            {
                once.CopyTo(repeated, i * once.Length);
            }

            aes.Key = key.AsSpan(0, BlockLength).ToArray();
            var encrypted = aes.EncryptCbc(repeated, key.AsSpan(BlockLength, BlockLength), PaddingMode.None);
            key = Remainder(encrypted) switch
            {
                0 => SHA256.HashData(encrypted),
                1 => SHA384.HashData(encrypted),
                _ => SHA512.HashData(encrypted),
            };
            last = encrypted[^1];
            round++;
        }

        return key.AsSpan(0, KeyLength).ToArray();
    }

    /// <summary>Takes the first 16 bytes as a big-endian number modulo 3, which equals their byte sum modulo 3.</summary>
    /// <param name="encrypted">The round's encrypted bytes.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int Remainder(byte[] encrypted)
    {
        var sum = 0;
        for (var i = 0; i < BlockLength; i++)
        {
            sum += encrypted[i];
        }

        return sum % HashChoices;
    }

    /// <summary>Gets the Latin-1 bytes of text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>Makes a fixed byte sequence so the file is the same on every run.</summary>
    /// <param name="length">The length.</param>
    /// <param name="seed">The first byte.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Sequence(int length, byte seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(seed + i);
        }

        return bytes;
    }
}
