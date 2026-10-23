// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for revision 5 password normalisation and the AES padding fallback.</summary>
public sealed class PasswordAndPaddingTests
{
    /// <summary>The stored password: two ASCII letters.</summary>
    private const string StoredPassword = "fi";

    /// <summary>The same password typed as the "fi" ligature, which NFKC turns into two letters.</summary>
    private const string LigaturePassword = "ﬁ";

    /// <summary>A password that is not the stored one.</summary>
    private const string OtherPassword = "xy";

    /// <summary>The object number used for the stream samples.</summary>
    private const int StreamNumber = 4;

    /// <summary>The length of the first file id.</summary>
    private const int IdLength = 16;

    /// <summary>The length of an AES block.</summary>
    private const int BlockLength = 16;

    /// <summary>A pad length byte inside 1 to 16 that is not a valid PKCS7 pad for the sample.</summary>
    private const byte PlausiblePad = 3;

    /// <summary>The largest pad length byte that is dropped.</summary>
    private const byte LargestPad = 16;

    /// <summary>A final byte too big to be a pad length.</summary>
    private const byte ImplausiblePad = 17;

    /// <summary>Gets a plain text of one block whose last byte the tests replace.</summary>
    private static ReadOnlySpan<byte> Block => "ABCDEFGHIJKLMXYZ"u8;

    /// <summary>Gets a short plain text.</summary>
    private static ReadOnlySpan<byte> Short => "ABCDEFGHIJKLM"u8;

    /// <summary>A password typed with compatibility characters opens a file made with their NFKC form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Revision5PasswordIsNormalised()
    {
        var result = Open(LigaturePassword, out var handler);
        handler?.Dispose();

        await Assert.That(result).IsEqualTo(PdfSecurityResult.Success);
    }

    /// <summary>The stored form of the password opens the file too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Revision5PlainPasswordOpens()
    {
        var result = Open(StoredPassword, out var handler);
        handler?.Dispose();

        await Assert.That(result).IsEqualTo(PdfSecurityResult.Success);
    }

    /// <summary>A different password is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Revision5WrongPasswordIsRefused()
    {
        var result = Open(OtherPassword, out var handler);
        handler?.Dispose();

        await Assert.That(result).IsEqualTo(PdfSecurityResult.WrongPassword);
    }

    /// <summary>A last byte of 1 to 16 is dropped as a pad even when the pad is not valid PKCS7.</summary>
    /// <param name="pad">The last plain byte.</param>
    /// <param name="expectedLength">The length expected after the pad is dropped.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PlausiblePad, BlockLength - PlausiblePad)]
    [Arguments(LargestPad, 0)]
    [Arguments(ImplausiblePad, BlockLength)]
    [Arguments((byte)0, BlockLength)]
    public async Task InvalidPaddingFallsBackToPlausiblePad(byte pad, int expectedLength)
    {
        var plain = Block.ToArray();
        plain[^1] = pad;
        var stored = EncryptionSetup.EncryptAesWithoutPadding(Key(out var handler), plain);

        using (handler)
        {
            var decrypted = handler!.DecryptStream(new(StreamNumber, 0), stored);

            await Assert.That(decrypted.Length).IsEqualTo(expectedLength);
            await Assert.That(decrypted.AsSpan().SequenceEqual(plain.AsSpan(0, expectedLength))).IsTrue();
        }
    }

    /// <summary>Correctly padded data still decrypts through the normal path.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidPaddingStillDecrypts()
    {
        _ = Key(out var handler);
        using (handler)
        {
            var plain = Short.ToArray();
            var stored = handler!.EncryptStream(new(StreamNumber, 0), plain);

            await Assert.That(handler.DecryptStream(new(StreamNumber, 0), stored)).IsEquivalentTo(plain);
        }
    }

    /// <summary>Opens a revision 5 dictionary made for <see cref="StoredPassword"/>.</summary>
    /// <param name="password">The password to try.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The outcome.</returns>
    private static PdfSecurityResult Open(string password, out PdfSecurityHandler? handler)
    {
        var text = EncryptionSetup.Revision5Text(StoredPassword, out _);
        return PdfSecurityHandler.TryCreate(Parse(text), new byte[IdLength], password, out handler);
    }

    /// <summary>Opens a revision 5 handler and gives its file key.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The file key.</returns>
    private static byte[] Key(out PdfSecurityHandler? handler)
    {
        var text = EncryptionSetup.Revision5Text(StoredPassword, out var key);
        _ = PdfSecurityHandler.TryCreate(Parse(text), new byte[IdLength], StoredPassword, out handler);
        return key;
    }

    /// <summary>Parses a dictionary.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Parse(string text) =>
        new PdfParser(Encoding.ASCII.GetBytes(text), 0, null, new()).ParseValue().AsDictionary()!;
}
