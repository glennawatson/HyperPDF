// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Pdf20;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for encrypting a file with passwords on save (ISO 32000-2 7.6.4, ISO/TS 32003): the writer's output read back by the library and by qpdf.</summary>
public sealed class PdfEncryptionWriteTests
{
    /// <summary>The mask of the bits a writer always sets: 7, 8 and 13 to 32.</summary>
    private const int ReservedMask = unchecked((int)0xFFFFF0C0);

    /// <summary>The mask of the two low bits, which shall be zero.</summary>
    private const int LowBits = 3;

    /// <summary>The first byte the fixed random source hands out in the repeat test.</summary>
    private const byte FirstSeed = 1;

    /// <summary>A second seed, to show that other bytes give other entries.</summary>
    private const byte SecondSeed = 77;

    /// <summary>The revision of AES-128.</summary>
    private const int Revision4 = 4;

    /// <summary>The revision of AES-256.</summary>
    private const int Revision6 = 6;

    /// <summary>The revision of AES-GCM.</summary>
    private const int Revision7 = 7;

    /// <summary>The PDF/A part the refused file claims.</summary>
    private const int PdfAPart = 2;

    /// <summary>A user password for the repeat test.</summary>
    private const string FixedUser = "user";

    /// <summary>An owner password for the repeat test.</summary>
    private const string FixedOwner = "owner";

    /// <summary>A password that opens nothing.</summary>
    private const string Nobody = "nobody";

    /// <summary>The user and owner passwords open a file in every algorithm and layout, and the text reads back.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="objectStreams">Whether the file uses object streams.</param>
    /// <param name="revision">The revision the algorithm writes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, true, Revision4)]
    [Arguments(PdfEncryptionAlgorithm.Aes128, false, Revision4)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, true, Revision6)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, false, Revision6)]
    [Arguments(PdfEncryptionAlgorithm.Aes256Gcm, true, Revision7)]
    [Arguments(PdfEncryptionAlgorithm.Aes256Gcm, false, Revision7)]
    public async Task UserAndOwnerPasswordsOpenTheFile(PdfEncryptionAlgorithm algorithm, bool objectStreams, int revision)
    {
        SkipWithoutGcm(algorithm);
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), objectStreams);

        await Assert.That(file.AsSpan().IndexOf(EncryptionTestFiles.PageTextBytes)).IsEqualTo(-1);
        await Assert.That(EncryptionTestFiles.ReadText(file, EncryptionTestFiles.UserPassword)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.ReadText(file, EncryptionTestFiles.OwnerPassword)).Contains(EncryptionTestFiles.PageText);
        var user = EncryptionTestFiles.Inspect(file, EncryptionTestFiles.UserPassword);
        var owner = EncryptionTestFiles.Inspect(file, EncryptionTestFiles.OwnerPassword);
        await Assert.That(user.Revision).IsEqualTo(revision);
        await Assert.That(user.Role).IsEqualTo(PdfSecurityRole.User);
        await Assert.That(owner.Role).IsEqualTo(PdfSecurityRole.Owner);
    }

    /// <summary>A wrong or missing password is refused with the password error.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="password">The password tried.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, "wrong")]
    [Arguments(PdfEncryptionAlgorithm.Aes128, null)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, "wrong")]
    [Arguments(PdfEncryptionAlgorithm.Aes256, null)]
    [Arguments(PdfEncryptionAlgorithm.Aes256Gcm, "wrong")]
    public async Task WrongPasswordIsRefused(PdfEncryptionAlgorithm algorithm, string? password)
    {
        SkipWithoutGcm(algorithm);
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), true);

        var exception = await Assert.That(() => StoreOpening.Open(file, password)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Password);
    }

    /// <summary>A file with no user password opens without one, and still has an owner.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128)]
    [Arguments(PdfEncryptionAlgorithm.Aes256)]
    public async Task EmptyUserPasswordOpensWithoutAsking(PdfEncryptionAlgorithm algorithm)
    {
        var options = new PdfEncryptionOptions { Algorithm = algorithm, OwnerPassword = EncryptionTestFiles.OwnerPassword, Permissions = PdfAccessPermissions.Print };
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), options, true);

        await Assert.That(EncryptionTestFiles.ReadText(file, null)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.Inspect(file, null).Role).IsEqualTo(PdfSecurityRole.User);
        await Assert.That(EncryptionTestFiles.Inspect(file, EncryptionTestFiles.OwnerPassword).Role).IsEqualTo(PdfSecurityRole.Owner);
    }

    /// <summary>The permissions are stored as the standard says: reserved bits set, bits 1 and 2 clear, accessibility kept in PDF 2.0.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="expected">The permissions a user sees.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent | PdfAccessPermissions.ExtractForAccessibility)]
    [Arguments(PdfEncryptionAlgorithm.Aes256Gcm, PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent | PdfAccessPermissions.ExtractForAccessibility)]
    public async Task PermissionsAreWritten(PdfEncryptionAlgorithm algorithm, PdfAccessPermissions expected)
    {
        SkipWithoutGcm(algorithm);
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), true);

        var word = EncryptionTestFiles.Inspect(file, EncryptionTestFiles.UserPassword).Permissions;

        await Assert.That(PdfPermissionBits.FromWord(word)).IsEqualTo(expected);
        await Assert.That(word & ReservedMask).IsEqualTo(ReservedMask);
        await Assert.That(word & LowBits).IsEqualTo(0);
    }

    /// <summary>High quality printing is cleared along with printing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HighQualityPrintingNeedsPrinting()
    {
        var onlyHighQuality = new PdfEncryptionOptions
        {
            UserPassword = EncryptionTestFiles.UserPassword,
            OwnerPassword = EncryptionTestFiles.OwnerPassword,
            Permissions = PdfAccessPermissions.PrintHighQuality | PdfAccessPermissions.FillForms,
        };
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), onlyHighQuality, true);

        var seen = PdfPermissionBits.FromWord(EncryptionTestFiles.Inspect(file, EncryptionTestFiles.UserPassword).Permissions);

        await Assert.That((seen & PdfAccessPermissions.PrintHighQuality) != 0).IsFalse();
        await Assert.That((seen & PdfAccessPermissions.FillForms) != 0).IsTrue();
    }

    /// <summary>The header names a version the cipher is allowed in: 1.6 for AES-128, 2.0 for AES-256 and AES-GCM.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="header">The expected header.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, "%PDF-1.6")]
    [Arguments(PdfEncryptionAlgorithm.Aes256, "%PDF-2.0")]
    [Arguments(PdfEncryptionAlgorithm.Aes256Gcm, "%PDF-2.0")]
    public async Task HeaderVersionFitsTheCipher(PdfEncryptionAlgorithm algorithm, string header)
    {
        SkipWithoutGcm(algorithm);
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), true);

        await Assert.That(Encoding.ASCII.GetString(file, 0, header.Length)).IsEqualTo(header);
    }

    /// <summary>An AES-GCM file declares the ISO/TS 32003 extension in its catalog.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AesGcmFileDeclaresItsExtension()
    {
        SkipWithoutGcm(PdfEncryptionAlgorithm.Aes256Gcm);
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(PdfEncryptionAlgorithm.Aes256Gcm), true);

        using var store = StoreOpening.Open(file, EncryptionTestFiles.OwnerPassword);

        await Assert.That(AesGcmExtension.IsDeclared(store.Catalog, store.Names)).IsTrue();
        await Assert.That(store.Security!.UsesAesGcm).IsTrue();
    }

    /// <summary>With metadata left clear, the XMP is readable in the file and carries the Identity crypt filter; with it encrypted, it is not readable.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, true)]
    [Arguments(PdfEncryptionAlgorithm.Aes128, false)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, true)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, false)]
    public async Task MetadataIsEncryptedOnlyWhenAsked(PdfEncryptionAlgorithm algorithm, bool encryptMetadata)
    {
        var chosen = new PdfEncryptionOptions
        {
            Algorithm = algorithm,
            UserPassword = EncryptionTestFiles.UserPassword,
            OwnerPassword = EncryptionTestFiles.OwnerPassword,
            EncryptMetadata = encryptMetadata,
        };
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.WithMetadata(), chosen, false);

        var visible = file.AsSpan().IndexOf(EncryptionTestFiles.MetadataMarkerBytes) >= 0;
        using var store = StoreOpening.Open(file, EncryptionTestFiles.UserPassword);
        var metadata = Encoding.UTF8.GetString(store.Catalog.Get(KnownName.Metadata).AsStream()!.DecodeToArray());

        await Assert.That(visible).IsEqualTo(!encryptMetadata);
        await Assert.That(metadata).Contains(EncryptionTestFiles.MetadataMarker);
        await Assert.That(store.Security!.EncryptMetadata).IsEqualTo(encryptMetadata);
        await Assert.That(file.AsSpan().IndexOf("[/Crypt]"u8) >= 0).IsEqualTo(!encryptMetadata);
    }

    /// <summary>The same random bytes make the same entries, other bytes make others, and the reader opens what they describe.</summary>
    /// <param name="revision">The revision.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Revision4)]
    [Arguments(Revision6)]
    [Arguments(Revision7)]
    public async Task FixedRandomBytesGiveRepeatableEntries(int revision)
    {
        var id = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6 };
        var permissions = PdfPermissionBits.ToStandard(PdfAccessPermissions.Print, false);
        var first = StandardEncryptionWriter.Create(revision, FixedUser, FixedOwner, permissions, true, id, new SequenceRandom(FirstSeed).Fill);
        var again = StandardEncryptionWriter.Create(revision, FixedUser, FixedOwner, permissions, true, id, new SequenceRandom(FirstSeed).Fill);
        var other = StandardEncryptionWriter.Create(revision, FixedUser, FixedOwner, permissions, true, id, new SequenceRandom(SecondSeed).Fill);

        await Assert.That(again.FileKey.AsSpan().SequenceEqual(first.FileKey)).IsTrue();
        await Assert.That(again.User.AsSpan().SequenceEqual(first.User)).IsTrue();
        await Assert.That(again.Owner.AsSpan().SequenceEqual(first.Owner)).IsTrue();
        if (revision > Revision4)
        {
            await Assert.That(other.User.AsSpan().SequenceEqual(first.User)).IsFalse();
        }

        await Assert.That(Authenticates(first, id, FixedUser)).IsEqualTo(PdfSecurityRole.User);
        await Assert.That(Authenticates(first, id, FixedOwner)).IsEqualTo(PdfSecurityRole.Owner);
        await Assert.That(Authenticates(first, id, Nobody)).IsNull();
    }

    /// <summary>Encrypting a signed file is refused with the reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedFileIsRefused()
    {
        var signed = Pdf20TestPdf.Permissions("UR3");

        var exception = await Assert.That(() => EncryptionTestFiles.Encrypt(signed, Restricted(PdfEncryptionAlgorithm.Aes256), true)).Throws<PdfException>();

        await Assert.That(exception!.Message).Contains("signed");
    }

    /// <summary>Encrypting a file that claims PDF/A is refused with the reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAFileIsRefused()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PdfAPart, "B", null);

        var exception = await Assert.That(() => EncryptionTestFiles.Encrypt(pdf.ToBytes(), Restricted(PdfEncryptionAlgorithm.Aes256), true)).Throws<PdfException>();

        await Assert.That(exception!.Message).Contains("PDF/A");
    }

    /// <summary>Restrictions need an owner password that differs from the user password.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestrictionsNeedASeparateOwnerPassword()
    {
        var sameAsUser = new PdfEncryptionOptions { UserPassword = EncryptionTestFiles.UserPassword, OwnerPassword = EncryptionTestFiles.UserPassword, Permissions = PdfAccessPermissions.Print };
        var none = new PdfEncryptionOptions { UserPassword = EncryptionTestFiles.UserPassword, Permissions = PdfAccessPermissions.Print };

        await Assert.That(() => EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), sameAsUser, true)).Throws<ArgumentException>();
        await Assert.That(() => EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), none, true)).Throws<ArgumentException>();
    }

    /// <summary>A save cannot both encrypt and remove encryption, and a reproducible save cannot encrypt.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContradictoryOptionsAreRefused()
    {
        var plain = EncryptionTestFiles.Plain();
        var encryption = Restricted(PdfEncryptionAlgorithm.Aes256);
        using var store = StoreOpening.Open(plain, null);

        await Assert.That(() => PdfCompactWriter.Save(store, new(true, true) { Encryption = encryption })).Throws<ArgumentException>();
        var reproducible = await Assert.That(() => PdfCompactWriter.Save(store, PdfCompactOptions.Default with { Encryption = encryption, Reproducible = true })).Throws<PdfException>();
        await Assert.That(reproducible!.Error).IsEqualTo(PdfError.Security);
    }

    /// <summary>A written file passes qpdf's check with each password, and qpdf reads back the revision and permissions.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="objectStreams">Whether the file uses object streams.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="method">The method qpdf names.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, true, Revision4, "AESv2")]
    [Arguments(PdfEncryptionAlgorithm.Aes128, false, Revision4, "AESv2")]
    [Arguments(PdfEncryptionAlgorithm.Aes256, true, Revision6, "AESv3")]
    [Arguments(PdfEncryptionAlgorithm.Aes256, false, Revision6, "AESv3")]
    public async Task QpdfChecksAndDescribesTheFile(PdfEncryptionAlgorithm algorithm, bool objectStreams, int revision, string method, CancellationToken cancellationToken)
    {
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), objectStreams);
        var name = $"enc-{algorithm}-{(objectStreams ? "objstm" : "classic")}";

        foreach (var password in new[] { EncryptionTestFiles.UserPassword, EncryptionTestFiles.OwnerPassword })
        {
            var check = await QpdfCommand.RunAsync(name, file, ["--check", $"--password={password}", QpdfCommand.InputToken], cancellationToken);
            if (check is null)
            {
                Skip.Test("qpdf is not installed.");
                return;
            }

            await Assert.That(check.Value.Output).Contains("No syntax or stream encoding errors found");
            await Assert.That(check.Value.Output).DoesNotContain("WARNING");
        }

        var shown = await QpdfCommand.RunAsync(name, file, ["--show-encryption", $"--password={EncryptionTestFiles.OwnerPassword}", QpdfCommand.InputToken], cancellationToken);

        await Assert.That(shown!.Value.Output).Contains($"R = {revision}");
        await Assert.That(shown.Value.Output).Contains($"stream encryption method: {method}");
        await Assert.That(shown.Value.Output).Contains($"string encryption method: {method}");
        await Assert.That(shown.Value.Output).Contains("extract for any purpose: allowed");
        await Assert.That(shown.Value.Output).Contains("modify annotations: not allowed");
        await Assert.That(shown.Value.Output).Contains("print low resolution: allowed");
    }

    /// <summary>Qpdf decrypts the file independently, and the decrypted file shows the same page text.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128)]
    [Arguments(PdfEncryptionAlgorithm.Aes256)]
    public async Task QpdfDecryptsTheFile(PdfEncryptionAlgorithm algorithm, CancellationToken cancellationToken)
    {
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(algorithm), true);

        string[] arguments = [$"--password={EncryptionTestFiles.UserPassword}", "--decrypt", QpdfCommand.InputToken, QpdfCommand.OutputToken];
        var result = await QpdfCommand.RunAsync($"dec-{algorithm}", file, arguments, cancellationToken);
        if (result is null)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.Value.OutputFile).IsNotNull();
        await Assert.That(EncryptionTestFiles.ReadText(result.Value.OutputFile!, null)).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>Skips a test on a platform without AES-GCM.</summary>
    /// <param name="algorithm">The algorithm the test uses.</param>
    private static void SkipWithoutGcm(PdfEncryptionAlgorithm algorithm)
    {
        if (algorithm == PdfEncryptionAlgorithm.Aes256Gcm && !AesGcm.IsSupported)
        {
            Skip.Test("AES-GCM is not available on this platform.");
        }
    }

    /// <summary>Gets options with both passwords and the permissions print and copy.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The options.</returns>
    private static PdfEncryptionOptions Restricted(PdfEncryptionAlgorithm algorithm) => new()
    {
        Algorithm = algorithm,
        UserPassword = EncryptionTestFiles.UserPassword,
        OwnerPassword = EncryptionTestFiles.OwnerPassword,
        Permissions = PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent,
    };

    /// <summary>Tries a password against a dictionary built from entries, as a reader would.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="id">The first file id.</param>
    /// <param name="password">The password.</param>
    /// <returns>The role the password gives, or <see langword="null"/> when it is refused.</returns>
    private static PdfSecurityRole? Authenticates(StandardEncryptionEntries entries, byte[] id, string password)
    {
        using var store = StoreOpening.Open(EncryptionTestFiles.Plain(), null);
        var algorithm = entries.Revision switch
        {
            Revision4 => PdfEncryptionAlgorithm.Aes128,
            Revision6 => PdfEncryptionAlgorithm.Aes256,
            _ => PdfEncryptionAlgorithm.Aes256Gcm,
        };
        var dictionary = PdfEncryptionDictionaries.Standard(store.Names, algorithm, entries, true);
        var result = PdfSecurityHandler.TryCreate(dictionary, id, password, out var handler);
        using (handler)
        {
            return result == PdfSecurityResult.Success ? handler!.Role : null;
        }
    }
}
