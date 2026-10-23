// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// An /Encrypt dictionary for an empty user password, the file key worked out independently of the library, and the library's
/// handler for it. Tests use it to encrypt streams the way a writer would and to check what the reader makes of them.
/// </summary>
internal sealed class EncryptionSetup : IDisposable
{
    /// <summary>The length of the padded password, /O and the start of /U.</summary>
    private const int PaddedLength = 32;

    /// <summary>The length of the first file id.</summary>
    private const int IdLength = 16;

    /// <summary>The length of a revision 4 file key and of an AES block.</summary>
    private const int BlockLength = 16;

    /// <summary>The length of an AES-256 file key and of a SHA-256 hash.</summary>
    private const int Sha256Length = 32;

    /// <summary>The length of /U and /O in revisions 5 and 6.</summary>
    private const int UserLength = 48;

    /// <summary>The length of a salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The extra MD5 rounds of revision 4.</summary>
    private const int Md5Rounds = 50;

    /// <summary>The RC4 rounds after the first when /U is worked out.</summary>
    private const int UserRounds = 19;

    /// <summary>The permissions written: everything allowed.</summary>
    private const int Permissions = -4;

    /// <summary>The bytes of an object number that go into an object key.</summary>
    private const int NumberBytes = 3;

    /// <summary>The bytes of a generation that go into an object key.</summary>
    private const int GenerationBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The seed of the owner string.</summary>
    private const byte OwnerSeed = 1;

    /// <summary>The seed of the file id.</summary>
    private const byte IdSeed = 0x40;

    /// <summary>The seed of the AES-256 file key.</summary>
    private const byte KeySeed = 0x10;

    /// <summary>The seed of the validation salt.</summary>
    private const byte ValidationSeed = 0x20;

    /// <summary>The seed of the key salt.</summary>
    private const byte KeySaltSeed = 0x30;

    /// <summary>The seed of filler bytes that no check reads.</summary>
    private const byte FillerSeed = 0x70;

    /// <summary>The crypt filters of a revision 4 file: AES-128 as the default and an RC4 filter named Other.</summary>
    private const string Revision4Filters =
        "/CF << /StdCF << /CFM /AESV2 /Length 16 >> /Other << /CFM /V2 /Length 16 >> >> /StmF /StdCF /StrF /StdCF";

    /// <summary>The crypt filters of a revision 5 file: AES-256 for everything.</summary>
    private const string Revision5Filters = "/CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF";

    /// <summary>The padding mixed into passwords.</summary>
    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>The handler the library built from the dictionary.</summary>
    private readonly PdfSecurityHandler _handler;

    /// <summary>Initializes a new instance of the <see cref="EncryptionSetup"/> class.</summary>
    /// <param name="text">The /Encrypt dictionary as PDF text.</param>
    /// <param name="fileId">The first file id.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="names">The name table the handler's dictionary was parsed with.</param>
    /// <exception cref="InvalidOperationException">The library does not accept the dictionary.</exception>
    private EncryptionSetup(string text, byte[] fileId, byte[] fileKey, PdfNameTable names)
    {
        EncryptText = text;
        FileId = fileId;
        FileKey = fileKey;
        var dictionary = new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, names).ParseValue().AsDictionary()!;
        EncryptDictionary = dictionary;
        var result = PdfSecurityHandler.TryCreate(dictionary, fileId, string.Empty, out var handler);
        _handler = handler ?? throw new InvalidOperationException($"The test dictionary was not accepted: {result}.");
    }

    /// <summary>Gets the /Encrypt dictionary as PDF text.</summary>
    public string EncryptText { get; }

    /// <summary>Gets the first file id.</summary>
    public byte[] FileId { get; }

    /// <summary>Gets the file key worked out by the test.</summary>
    public byte[] FileKey { get; }

    /// <summary>Gets the /Encrypt dictionary, parsed with the name table given to the factory.</summary>
    public PdfDictionary EncryptDictionary { get; }

    /// <summary>Gets the library's handler for the dictionary.</summary>
    public PdfSecurityHandler Handler => _handler;

    /// <summary>Gets the file id as hexadecimal text.</summary>
    public string FileIdHex => Convert.ToHexString(FileId);

    /// <summary>Releases the handler.</summary>
    public void Dispose() => _handler.Dispose();

    /// <summary>Creates a revision 4 setup: AES-128 for streams and strings, plus a named RC4 filter called Other.</summary>
    /// <param name="encryptMetadata">Whether metadata streams are encrypted.</param>
    /// <param name="names">The name table of the document that will use the handler.</param>
    /// <returns>The setup.</returns>
    internal static EncryptionSetup Revision4(bool encryptMetadata, PdfNameTable names)
    {
        var owner = Sequence(PaddedLength, OwnerSeed);
        var id = Sequence(IdLength, IdSeed);
        var key = Revision4Key(owner, id, encryptMetadata);
        var user = Revision4User(key, id);
        var metadata = encryptMetadata ? string.Empty : " /EncryptMetadata false";
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Filter /Standard /V 4 /R 4 /Length 128 {Revision4Filters}{metadata} /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /P {Permissions} >>");
        return new(text, id, key, names);
    }

    /// <summary>Writes a revision 5 /Encrypt dictionary whose user password is the given text, hashed as its exact UTF-8 bytes.</summary>
    /// <param name="storedPassword">The password the file was made with.</param>
    /// <param name="fileKey">The AES-256 file key the dictionary protects.</param>
    /// <returns>The dictionary as PDF text.</returns>
    internal static string Revision5Text(string storedPassword, out byte[] fileKey)
    {
        fileKey = Sequence(Sha256Length, KeySeed);
        var password = Encoding.UTF8.GetBytes(storedPassword);
        var validationSalt = Sequence(SaltLength, ValidationSeed);
        var keySalt = Sequence(SaltLength, KeySaltSeed);
        byte[] validationInput = [.. password, .. validationSalt];
        byte[] keyInput = [.. password, .. keySalt];
        byte[] user = [.. SHA256.HashData(validationInput), .. validationSalt, .. keySalt];
        var intermediate = SHA256.HashData(keyInput);
        using var aes = Aes.Create();
        aes.Key = intermediate;
        var wrapped = aes.EncryptCbc(fileKey, new byte[BlockLength], PaddingMode.None);
        var owner = Convert.ToHexString(Sequence(UserLength, FillerSeed));
        var ownerKey = Convert.ToHexString(Sequence(Sha256Length, FillerSeed));
        var perms = Convert.ToHexString(Sequence(BlockLength, FillerSeed));
        var userHex = Convert.ToHexString(user);
        var wrappedHex = Convert.ToHexString(wrapped);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 {Revision5Filters} /O <{owner}> /U <{userHex}> /OE <{ownerKey}> /UE <{wrappedHex}> /Perms <{perms}> /P {Permissions} >>");
    }

    /// <summary>Encrypts a block-aligned plain text with AES-CBC and no padding, putting the IV first.</summary>
    /// <param name="key">The 32-byte key.</param>
    /// <param name="plain">The plain text, a multiple of 16 bytes.</param>
    /// <returns>The IV followed by the cipher text.</returns>
    internal static byte[] EncryptAesWithoutPadding(byte[] key, byte[] plain)
    {
        var iv = Sequence(BlockLength, FillerSeed);
        using var aes = Aes.Create();
        aes.Key = key;
        return [.. iv, .. aes.EncryptCbc(plain, iv, PaddingMode.None)];
    }

    /// <summary>Encrypts a stream with RC4 and the object key, as the V2 crypt filter does.</summary>
    /// <param name="id">The stream's object id.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    internal byte[] EncryptRc4(PdfObjectId id, byte[] data)
    {
        var input = new byte[FileKey.Length + NumberBytes + GenerationBytes];
        FileKey.CopyTo(input, 0);
        for (var i = 0; i < NumberBytes; i++)
        {
            input[FileKey.Length + i] = (byte)(id.Number >> (i * ByteBits));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(FileKey.Length + NumberBytes), (ushort)id.Generation);
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(input, digest);
        var result = data.ToArray();
        Rc4.Apply(digest.AsSpan(0, Math.Min(FileKey.Length + NumberBytes + GenerationBytes, Md5.HashLength)), result);
        return result;
    }

    /// <summary>Makes bytes that count up from a seed.</summary>
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

    /// <summary>Works out the file key of an empty user password (algorithm 2, revision 4).</summary>
    /// <param name="owner">The /O value.</param>
    /// <param name="id">The first file id.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The 16-byte key.</returns>
    private static byte[] Revision4Key(byte[] owner, byte[] id, bool encryptMetadata)
    {
        var input = new List<byte>(Padding);
        input.AddRange(owner);
        var permissions = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(permissions, Permissions);
        input.AddRange(permissions);
        input.AddRange(id);
        if (!encryptMetadata)
        {
            input.AddRange(new byte[sizeof(int)].Select(static _ => byte.MaxValue));
        }

        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(input.ToArray(), digest);
        for (var i = 0; i < Md5Rounds; i++)
        {
            _ = Md5.HashData(digest, digest);
        }

        return digest;
    }

    /// <summary>Works out /U (algorithm 5): the padding and file id hashed, then run through RC4 twenty times.</summary>
    /// <param name="key">The file key.</param>
    /// <param name="id">The first file id.</param>
    /// <returns>The 32-byte /U value, whose last half is filler.</returns>
    private static byte[] Revision4User(byte[] key, byte[] id)
    {
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData([.. Padding, .. id], digest);
        Rc4.Apply(key, digest);
        for (var round = 1; round <= UserRounds; round++)
        {
            var roundKey = key.Select(b => (byte)(b ^ round)).ToArray();
            Rc4.Apply(roundKey, digest);
        }

        return [.. digest, .. new byte[PaddedLength - Md5.HashLength]];
    }
}
