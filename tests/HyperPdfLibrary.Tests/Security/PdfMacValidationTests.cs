// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Signing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for checking the PDF MAC (ISO/TS 32004:2024) of files made by an independent builder.</summary>
public sealed class PdfMacValidationTests
{
    /// <summary>The ordinary digest.</summary>
    private const string Sha256 = MacToken.Sha256;

    /// <summary>The title of the second revision.</summary>
    private const string SecondTitle = "Second title";

    /// <summary>The keyword that starts a stream's data.</summary>
    private const string StreamKeyword = "stream\n";

    /// <summary>The offset into an encrypted content stream of a byte that is cipher text, past the 16-byte IV.</summary>
    private const int CipherOffset = 20;

    /// <summary>The number of hexadecimal digits that spell a byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The length of the key in RFC 5869 test case 1.</summary>
    private const int Rfc5869KeyLength = 22;

    /// <summary>The output length of RFC 5869 test case 1.</summary>
    private const int Rfc5869OutputLength = 42;

    /// <summary>The key byte of RFC 5869 test case 1.</summary>
    private const byte Rfc5869KeyByte = 0x0B;

    /// <summary>The number of revisions of a file with a signed revision between two unsigned ones.</summary>
    private const int ThreeRevisions = 3;

    /// <summary>The number of revisions of a file with one update.</summary>
    private const int TwoRevisions = 2;

    /// <summary>Gets some bytes to digest.</summary>
    private static byte[] SampleData { get; } = [1, DigitsPerByte, ThreeRevisions];

    /// <summary>Gets the signer, made once because RSA key generation is slow.</summary>
    private static X509Certificate2 Signer { get; } = PdfSigning.CreateCertificate("Mac Signer", null, false, TimeProvider.System);

    /// <summary>Gets an RSA recipient for the public-key handler.</summary>
    private static X509Certificate2 Recipient { get; } = PdfSigning.CreateCertificate("Mac Recipient", null, false, TimeProvider.System);

    /// <summary>A standalone token in an AES-256 CBC file is valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandaloneTokenIsValid()
    {
        var report = Check(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.IsIntact).IsTrue();
        await Assert.That(report.Revisions.Count).IsEqualTo(1);
        await Assert.That(report.Revisions[0].Location).IsEqualTo(PdfMacLocation.Standalone);
    }

    /// <summary>A standalone token in an AES-GCM file (ISO/TS 32003) is valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AesGcmFileTokenIsValid()
    {
        if (!AesGcm.IsSupported)
        {
            Skip.Test("AES-GCM is not available on this platform.");
        }

        var report = Check(MacPdf.Create(MacProtection.Standard(true, MacProtection.MacRequired), Sha256, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
    }

    /// <summary>Every digest of Table 8 gives a valid token, or is skipped where the platform lacks SHA-3.</summary>
    /// <param name="digest">The digest name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("SHA-256")]
    [Arguments("SHA-384")]
    [Arguments("SHA-512")]
    [Arguments("SHA3-256")]
    [Arguments("SHA3-384")]
    [Arguments("SHA3-512")]
    public async Task EveryTableEightDigestIsChecked(string digest)
    {
        if (digest.StartsWith("SHA3", StringComparison.Ordinal) && !SHA3_256.IsSupported)
        {
            Skip.Test("SHA-3 is not available on this platform.");
        }

        var report = Check(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), digest, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
    }

    /// <summary>A file with ordinary encryption and no PDF MAC has nothing to check and is not marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryEncryptedFileIsNotApplicable()
    {
        var report = Check(AesGcmPdf.Create(AesGcmLayout.Everything, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.NotApplicable);
        await Assert.That(report.NeedsNotice).IsFalse();
        await Assert.That(report.IsIntact).IsFalse();
        await Assert.That(report.Summary).IsEqualTo(string.Empty);
    }

    /// <summary>A file that is not encrypted has nothing to check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainFileIsNotApplicable()
    {
        using var document = PdfDocumentReader.Open(PublicKeyPdf.Plain(), null);

        await Assert.That(PdfDocumentMac.GetIntegrity(document).Status).IsEqualTo(PdfMacStatus.NotApplicable);
    }

    /// <summary>A permission bit of one with no token present does not require one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionalMacIsNotRequired()
    {
        var report = Check(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacNotRequired), Sha256, false));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.NotApplicable);
    }

    /// <summary>A token that the permissions do not require is still checked when it is there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionalMacIsCheckedWhenPresent()
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacNotRequired), Sha256, true);
        file[ContentCipherOffset(file)] ^= 1;

        await Assert.That(Check(file).Status).IsEqualTo(PdfMacStatus.Invalid);
    }

    /// <summary>Two revisions each carry their own token and both are valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryRevisionIsChecked()
    {
        var report = Check(TwoRevisionFile(MacProtection.Standard(false, MacProtection.MacRequired)));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.Revisions.Count).IsEqualTo(TwoRevisions);
        await Assert.That(report.Revisions.All(static revision => revision.Status == PdfMacStatus.Valid)).IsTrue();
    }

    /// <summary>A signed revision with the token attached to its signature is valid, and so are the revisions around it; the signature still verifies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedRevisionWithAttachedTokenIsValid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var signed = MacPdf.AddSignedRevision(MacPdf.Create(protection, Sha256, true), protection, Signer, true);
        var report = Check(MacPdf.AddUnsignedRevision(signed, protection, "After signing", true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.Revisions.Count).IsEqualTo(ThreeRevisions);
        await Assert.That(report.Revisions.Select(static revision => revision.Location))
            .IsEquivalentTo([PdfMacLocation.Standalone, PdfMacLocation.AttachedToSignature, PdfMacLocation.Standalone]);
        await Assert.That(SignatureVerifies(signed)).IsTrue();
    }

    /// <summary>A changed byte of the document makes the token invalid in every revision it covers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedDocumentByteIsInvalid()
    {
        var file = TwoRevisionFile(MacProtection.Standard(false, MacProtection.MacRequired));
        file[ContentCipherOffset(file)] ^= 1;
        var report = Check(file);

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions.All(static revision => revision.Status == PdfMacStatus.Invalid)).IsTrue();
        await Assert.That(report.Revisions[0].Message).IsEqualTo(PdfMacMessages.BytesChanged);
        await Assert.That(report.IsIntact).IsFalse();
    }

    /// <summary>A changed byte in a later revision leaves the earlier revision valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangeInLaterRevisionLeavesEarlierOneValid()
    {
        var file = TwoRevisionFile(MacProtection.Standard(false, MacProtection.MacRequired));
        file[file.AsSpan().LastIndexOf("/Title <"u8) + "/Title <"u8.Length] ^= 1;
        var report = Check(file);

        await Assert.That(report.Revisions[0].Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.Revisions[1].Status).IsEqualTo(PdfMacStatus.Invalid);
    }

    /// <summary>A changed signature value makes a token attached to the signature invalid, though the document bytes are unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedSignatureValueIsInvalid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var file = MacPdf.AddSignedRevision(MacPdf.Create(protection, Sha256, true), protection, Signer, true);
        var contents = MacPdf.Find(file, MacPdf.ContentsMarker) + MacPdf.ContentsMarker.Length;
        var container = Container(file);
        var signature = new SignedCms();
        signature.Decode(container);
        var at = container.AsSpan().IndexOf(signature.SignerInfos[0].GetSignature());
        var digit = contents + (at * DigitsPerByte) + 1;
        file[digit] = (byte)(file[digit] == '0' ? '1' : '0');
        var report = Check(file);

        await Assert.That(report.Revisions[1].Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions[1].Message).IsEqualTo(PdfMacMessages.SignatureChanged);
    }

    /// <summary>A trailer that says the token is attached to a signature, over a signature without one, is missing its token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureWithoutTokenIsMissing()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var report = Check(MacPdf.AddSignedRevision(MacPdf.Create(protection, Sha256, true), protection, Signer, false));

        await Assert.That(report.Revisions[1].Status).IsEqualTo(PdfMacStatus.Missing);
    }

    /// <summary>A flipped bit in the MAC itself is a wrong MAC.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongMacIsInvalid()
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true);
        var last = MacPdf.LastMacOffset(file) + MacPdf.LastMacDigits(file) - 1;
        file[last] = (byte)(file[last] == '0' ? '1' : '0');
        var report = Check(file);

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions[0].Message).IsEqualTo(PdfMacMessages.ForToken(PdfMacTokenStatus.MacMismatch, default));
    }

    /// <summary>A token made with another key does not unwrap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TokenFromAnotherKeyIsInvalid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var other = protection with { FileKey = MacToken.Hkdf(protection.FileKey, MacProtection.Salt, [1], MacToken.KeyLength) };
        var report = Check(MacPdf.Create(other, Sha256, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions[0].Message).IsEqualTo(PdfMacMessages.ForToken(PdfMacTokenStatus.KeyUnwrapFailed, default));
    }

    /// <summary>A file whose permissions require a token, but whose trailer has none, is missing it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RequiredButAbsentTokenIsMissing()
    {
        var report = Check(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, false));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Missing);
        await Assert.That(report.Revisions[0].Message).IsEqualTo(PdfMacMessages.Required);
        await Assert.That(report.Summary).Contains("requires integrity protection");
    }

    /// <summary>An attacker who sets the permission bit in the clear /P cannot hide the requirement, because the encrypted /Perms still has it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrippedPermissionBitStillRequiresToken()
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, false);
        "/P -0004"u8.CopyTo(file.AsSpan(MacPdf.Find(file, "/P -4100")));

        await Assert.That(Check(file).Status).IsEqualTo(PdfMacStatus.Missing);
    }

    /// <summary>An update by a program that does not know PDF MAC leaves the old token covering only the earlier bytes: it is stale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StaleTokenIsInvalid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var report = Check(MacPdf.AddUnsignedRevision(MacPdf.Create(protection, Sha256, true), protection, SecondTitle, false));

        await Assert.That(report.Revisions[0].Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.Revisions[1].Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions[1].Message).IsEqualTo(PdfMacMessages.Stale);
    }

    /// <summary>A byte range that does not start at zero, or a location that is missing or unknown, is never valid.</summary>
    /// <param name="find">The text to change.</param>
    /// <param name="replace">The same-length replacement.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/ByteRange [0 ", "/ByteRange [1 ")]
    [Arguments("/MACLocation /Standalone", "/MACLocation /Standalonf")]
    [Arguments("/MACLocation /Standalone", "/MACLocatioN /Standalone")]
    public async Task BadLayoutIsNotValid(string find, string replace)
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true);
        Encoding.ASCII.GetBytes(replace).CopyTo(file, MacPdf.Find(file, find));
        var report = Check(file);

        await Assert.That(report.Status).IsNotEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.IsIntact).IsFalse();
    }

    /// <summary>A byte range with numbers beyond the file, overlapping, negative or not whole is rejected without reading outside the file.</summary>
    /// <param name="replacement">The text of the byte range.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[0 9223372036854775807 9223372036854775807 9]")]
    [Arguments("[0 5 4 9223372036854775807]")]
    [Arguments("[0 0 0 0]")]
    [Arguments("[0 -1 5 5]")]
    [Arguments("[0 5 5.5 6]")]
    public async Task OutOfRangeByteRangeIsInvalid(string replacement)
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true);
        var text = Encoding.Latin1.GetString(file);
        var start = text.IndexOf("/ByteRange ", StringComparison.Ordinal) + "/ByteRange ".Length;
        var end = text.IndexOf(']', start) + 1;
        var report = Check(Encoding.Latin1.GetBytes(string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(end))));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Invalid);
    }

    /// <summary>A /KDFSalt of the wrong size cannot support a token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongSaltSizeIsInvalid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var salt = Convert.ToHexString(MacProtection.Salt);
        var shortSalt = protection with { EncryptText = protection.EncryptText.Replace(salt, salt[DigitsPerByte..], StringComparison.Ordinal) };
        var report = Check(MacPdf.Create(shortSalt, Sha256, true));

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(report.Revisions[0].Message).IsEqualTo(PdfMacMessages.BadEncrypt);
    }

    /// <summary>A missing /KDFSalt cannot support a token that is there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingSaltIsInvalid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var noSalt = protection with { EncryptText = protection.EncryptText.Replace($"/KDFSalt <{Convert.ToHexString(MacProtection.Salt)}>", string.Empty, StringComparison.Ordinal) };

        await Assert.That(Check(MacPdf.Create(noSalt, Sha256, true)).Status).IsEqualTo(PdfMacStatus.Invalid);
    }

    /// <summary>A damaged /Perms block is ignored, and /P alone decides.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedPermsFallsBackToPermissions()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacNotRequired);
        var perms = protection.EncryptText[(protection.EncryptText.IndexOf("/Perms <", StringComparison.Ordinal) + "/Perms <".Length)..];
        perms = perms[..perms.IndexOf('>', StringComparison.Ordinal)];
        var zeros = new string('0', perms.Length);
        var damaged = protection with { EncryptText = protection.EncryptText.Replace(perms, zeros, StringComparison.Ordinal) };

        await Assert.That(Check(MacPdf.Create(damaged, Sha256, false)).Status).IsEqualTo(PdfMacStatus.NotApplicable);
    }

    /// <summary>A token damaged in any one bit is never accepted and never throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryDamagedBitIsRejected()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var token = MacToken.Build(protection.FileKey, MacProtection.Salt, Sha256, MacToken.Hash(Sha256, SampleData), null);
        var accepted = 0;
        for (var i = 0; i < token.Length * ByteBits; i++)
        {
            var copy = token.ToArray();
            copy[i / ByteBits] ^= (byte)(1 << (i % ByteBits));
            accepted += PdfMacTokenReader.Verify(copy, protection.FileKey, MacProtection.Salt).Status == PdfMacTokenStatus.Valid ? 1 : 0;
        }

        await Assert.That(PdfMacTokenReader.Verify(token, protection.FileKey, MacProtection.Salt).Status).IsEqualTo(PdfMacTokenStatus.Valid);
        await Assert.That(accepted).IsEqualTo(0);
    }

    /// <summary>Truncated, empty, oversized and trailing-data tokens, and a salt of the wrong size, are rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedAndPaddedTokensAreMalformed()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var token = MacToken.Build(protection.FileKey, MacProtection.Salt, Sha256, MacToken.Hash(Sha256, [1]), null);
        for (var length = 0; length < token.Length; length++)
        {
            await Assert.That(PdfMacTokenReader.Verify(token.AsSpan(0, length), protection.FileKey, MacProtection.Salt).Status).IsNotEqualTo(PdfMacTokenStatus.Valid);
        }

        await Assert.That(PdfMacTokenReader.Verify([.. token, 0], protection.FileKey, MacProtection.Salt).Status).IsEqualTo(PdfMacTokenStatus.Malformed);
        await Assert.That(PdfMacTokenReader.Verify(new byte[PdfLimits.MaxMacTokenLength + 1], protection.FileKey, MacProtection.Salt).Status).IsEqualTo(PdfMacTokenStatus.Malformed);
        await Assert.That(PdfMacTokenReader.Verify(token, protection.FileKey, MacProtection.Salt.AsSpan(DigitsPerByte)).Status).IsEqualTo(PdfMacTokenStatus.Malformed);
    }

    /// <summary>The public-key handler derives the key from its default crypt filter and a token over it is valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PublicKeyHandlerTokenIsValid()
    {
        var protection = MacProtection.PublicKey(Recipient, MacProtection.MacRequired);
        using var document = PdfDocumentReader.OpenWithCertificate(MacPdf.Create(protection, Sha256, true), Recipient);
        var report = PdfDocumentMac.GetIntegrity(document);

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(document.Objects.Security!.FileKey.ToArray()).IsEquivalentTo(protection.FileKey);
    }

    /// <summary>A public-key file whose recipient permissions clear bit 13 requires a token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PublicKeyPermissionsRequireToken()
    {
        var file = MacPdf.Create(MacProtection.PublicKey(Recipient, MacProtection.MacRequired), Sha256, false);
        using var document = PdfDocumentReader.OpenWithCertificate(file, Recipient);

        await Assert.That(PdfDocumentMac.GetIntegrity(document).Status).IsEqualTo(PdfMacStatus.Missing);
    }

    /// <summary>The report is read once and then reused, and the asynchronous form gives the same report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportIsCachedAndAsyncMatches()
    {
        using var document = PdfDocumentReader.Open(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true), AesGcmPdf.UserPassword);
        var first = PdfDocumentMac.GetIntegrity(document);
        var second = await PdfDocumentMac.GetIntegrityAsync(document, CancellationToken.None);

        await Assert.That(second).IsSameReferenceAs(first);
    }

    /// <summary>A cancelled token stops the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledCheckStops()
    {
        using var document = PdfDocumentReader.Open(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true), AesGcmPdf.UserPassword);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await PdfDocumentMac.GetIntegrityAsync(document, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The library's object identifier tables match the dotted forms of the standard, encoded independently.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectIdentifiersMatchTheStandard()
    {
        await Assert.That(PdfMacOids.AuthData.ToArray()).IsEquivalentTo(Contents(MacToken.AuthDataOid));
        await Assert.That(PdfMacOids.IntegrityInfo.ToArray()).IsEquivalentTo(Contents(MacToken.IntegrityInfoOid));
        await Assert.That(PdfMacOids.WrapKdf.ToArray()).IsEquivalentTo(Contents(MacToken.WrapKdfOid));
        await Assert.That(PdfMacOids.MacData.ToArray()).IsEquivalentTo(Contents(MacToken.MacDataOid));
        await Assert.That(PdfMacOids.Aes256Wrap.ToArray()).IsEquivalentTo(Contents(MacToken.Aes256WrapOid));
        await Assert.That(PdfMacOids.HmacSha256.ToArray()).IsEquivalentTo(Contents(MacToken.HmacSha256Oid));
        await Assert.That(PdfMacOids.ContentType.ToArray()).IsEquivalentTo(Contents(MacToken.ContentTypeOid));
        await Assert.That(PdfMacOids.MessageDigest.ToArray()).IsEquivalentTo(Contents(MacToken.MessageDigestOid));
        await Assert.That(PdfMacOids.AlgorithmProtection.ToArray()).IsEquivalentTo(Contents(MacToken.AlgorithmProtectionOid));
    }

    /// <summary>The test builder's HKDF gives the output of RFC 5869 test case 1, and the key the library derives matches the builder's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeyDerivationMatchesRfc5869AndTheBuilder()
    {
        var inputKey = Enumerable.Repeat(Rfc5869KeyByte, Rfc5869KeyLength).ToArray();
        var salt = Convert.FromHexString("000102030405060708090A0B0C");
        var info = Convert.FromHexString("F0F1F2F3F4F5F6F7F8F9");
        const string Expected = "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865";
        await Assert.That(Convert.ToHexString(MacToken.Hkdf(inputKey, salt, info, Rfc5869OutputLength))).IsEqualTo(Expected);
        await Assert.That(Convert.ToHexString(HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKey, Rfc5869OutputLength, salt, info))).IsEqualTo(Expected);

        var fileKey = AesGcmPdf.FileKey;
        var derived = new byte[PdfMacKeys.KeyEncryptionKeyLength];
        PdfMacKeys.DeriveKeyEncryptionKey(fileKey, MacProtection.Salt, derived);
        await Assert.That(derived).IsEquivalentTo(MacToken.Hkdf(fileKey, MacProtection.Salt, "PDFMAC"u8.ToArray(), MacToken.KeyLength));
    }

    /// <summary>Builds a file with a second revision that has its own token.</summary>
    /// <param name="protection">How the file is encrypted.</param>
    /// <returns>The file.</returns>
    private static byte[] TwoRevisionFile(MacProtection protection) =>
        MacPdf.AddUnsignedRevision(MacPdf.Create(protection, Sha256, true), protection, SecondTitle, true);

    /// <summary>Finds a cipher text byte of the first revision's content stream.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The offset.</returns>
    private static int ContentCipherOffset(byte[] file) => MacPdf.Find(file, StreamKeyword) + StreamKeyword.Length + CipherOffset;

    /// <summary>Gets the contents of an encoded object identifier, without its tag and length.</summary>
    /// <param name="dotted">The dotted form.</param>
    /// <returns>The contents.</returns>
    private static byte[] Contents(string dotted) => MacDer.Oid(dotted)[DigitsPerByte..];

    /// <summary>Reads the signature container out of a file's last signature, without its zero padding.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The container.</returns>
    private static byte[] Container(byte[] file)
    {
        var contents = MacPdf.Find(file, MacPdf.ContentsMarker) + MacPdf.ContentsMarker.Length;
        return SigningFixtures.Container(Convert.FromHexString(Encoding.ASCII.GetString(file, contents, MacPdf.SignatureDigits)));
    }

    /// <summary>Verifies the signature of a signed file as a CMS signature over its byte range, with the framework.</summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> when the signature checks and carries an unsigned attribute.</returns>
    private static bool SignatureVerifies(byte[] file)
    {
        var contents = MacPdf.Find(file, MacPdf.ContentsMarker) + MacPdf.ContentsMarker.Length;
        byte[] covered = [.. file.AsSpan(0, contents - 1), .. file.AsSpan(contents + MacPdf.SignatureDigits + 1)];
        var cms = new SignedCms(new(covered), true);
        cms.Decode(Container(file));
        cms.CheckSignature(true);
        return cms.SignerInfos[0].UnsignedAttributes.Count > 0;
    }

    /// <summary>Opens a file made by the builder and checks its PDF MAC.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The report.</returns>
    private static PdfMacReport Check(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, AesGcmPdf.UserPassword);
        return PdfDocumentMac.GetIntegrity(document);
    }
}
