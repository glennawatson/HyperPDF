// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for the permission, key length and password fixes.</summary>
public sealed class SecurityFixTests
{
    /// <summary>The length of the padded password and of /O and /U.</summary>
    private const int PaddedLength = 32;

    /// <summary>The length of the first file id.</summary>
    private const int IdLength = 16;

    /// <summary>The key length of revision 2 encryption.</summary>
    private const int KeyLength = 5;

    /// <summary>The permissions as a signed number.</summary>
    private const int SignedPermissions = -4;

    /// <summary>The same permissions as writers that store the unsigned value.</summary>
    private const long UnsignedPermissions = 4_294_967_292;

    /// <summary>The PDFDocEncoding byte of the bullet character.</summary>
    private const byte BulletByte = 0x80;

    /// <summary>The padding mixed into passwords.</summary>
    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>/P written as an unsigned number is read as its low 32 bits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsignedPermissionsAreAccepted()
    {
        var result = PdfSecurityHandler.TryCreate(Revision2(UnsignedPermissions, []), FileId(), null, out var handler);
        using (handler)
        {
            await Assert.That(result).IsEqualTo(PdfSecurityResult.Success);
            await Assert.That(handler!.Permissions).IsEqualTo(SignedPermissions);
        }
    }

    /// <summary>A password is encoded as PDFDocEncoding, so a bullet becomes byte 0x80.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordUsesPdfDocEncoding()
    {
        var dictionary = Revision2(SignedPermissions, [BulletByte]);
        var result = PdfSecurityHandler.TryCreate(dictionary, FileId(), "•", out var handler);
        handler?.Dispose();

        await Assert.That(result).IsEqualTo(PdfSecurityResult.Success);
    }

    /// <summary>A cipher that cannot use the key length gives an unsupported handler, not an exception.</summary>
    /// <param name="text">The /Encrypt dictionary text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<< /Filter /Standard /V 2 /R 3 /Length 256 /O <00> /U <00> /P -4 >>")]
    [Arguments("<< /Filter /Standard /V 4 /R 4 /StmF /StdCF /StrF /StdCF /CF << /StdCF << /CFM /AESV2 /Length 24 >> >> /O <00> /U <00> /P -4 >>")]
    public async Task InvalidKeyLengthIsUnsupported(string text)
    {
        var result = PdfSecurityHandler.TryCreate(Parse(text), FileId(), null, out _);

        await Assert.That(result).IsEqualTo(PdfSecurityResult.UnsupportedHandler);
    }

    /// <summary>A version 4 key length given in bytes is multiplied to bits, so 16 is a valid AES-128 key.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ByteKeyLengthIsAccepted()
    {
        const string Text = "<< /Filter /Standard /V 4 /R 4 /StmF /StdCF /StrF /StdCF /CF << /StdCF << /CFM /AESV2 /Length 16 >> >> /O <00> /U <00> /P -4 >>";
        var result = PdfSecurityHandler.TryCreate(Parse(Text), FileId(), null, out _);

        await Assert.That(result).IsEqualTo(PdfSecurityResult.WrongPassword);
    }

    /// <summary>Gets a fixed file id.</summary>
    /// <returns>The id.</returns>
    private static byte[] FileId() => new byte[IdLength];

    /// <summary>Parses a dictionary.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Parse(string text) =>
        new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, new()).ParseValue().AsDictionary()!;

    /// <summary>Creates a revision 2 /Encrypt dictionary for a user password.</summary>
    /// <param name="permissions">The /P value as written.</param>
    /// <param name="password">The user password bytes.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Revision2(long permissions, byte[] password)
    {
        var owner = new byte[PaddedLength];
        var padded = new byte[PaddedLength];
        password.CopyTo(padded, 0);
        Padding.AsSpan(0, PaddedLength - password.Length).CopyTo(padded.AsSpan(password.Length));
        var input = new byte[PaddedLength + PaddedLength + sizeof(int) + IdLength];
        padded.CopyTo(input, 0);
        owner.CopyTo(input, PaddedLength);
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(PaddedLength + PaddedLength), unchecked((int)permissions));
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(input, digest);
        var user = Padding.ToArray();
        Rc4.Apply(digest.AsSpan(0, KeyLength), user);
        return Parse($"<< /Filter /Standard /V 1 /R 2 /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /P {permissions} >>");
    }
}
