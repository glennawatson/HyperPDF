// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Pdf20;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for adding, changing and removing a document's protection, and the owner rules that guard it (ISO 32000-2 7.6.4).</summary>
public sealed class PdfDocumentProtectionTests
{
    /// <summary>The PDF/A part the refused file claims.</summary>
    private const int PdfAPart = 2;

    /// <summary>The new user password.</summary>
    private const string NewUserPassword = "new-reader";

    /// <summary>The new owner password.</summary>
    private const string NewOwnerPassword = "new-owner";

    /// <summary>A copy of an unprotected document is protected, opens with both passwords, and the open document is not changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainDocumentIsProtectedInACopy()
    {
        using var document = PdfDocumentReader.Open(EncryptionTestFiles.Plain(), null);
        await using var output = new MemoryStream();

        var result = PdfDocumentProtection.Protect(document, Restricted(), output);

        await Assert.That(result.Written).IsTrue();
        await Assert.That(document.IsEncrypted).IsFalse();
        await Assert.That(EncryptionTestFiles.ReadText(output.ToArray(), EncryptionTestFiles.UserPassword)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.Inspect(output.ToArray(), EncryptionTestFiles.OwnerPassword).Role).IsEqualTo(PdfSecurityRole.Owner);
    }

    /// <summary>The async form writes the same kind of copy, and a cancelled call writes nothing.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormWritesACopyAndStopsWhenCancelled(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EncryptionTestFiles.Plain(), null);
        await using var output = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await PdfDocumentProtection.ProtectAsync(document, Restricted(), output, cancellationToken);
        await Assert.That(result.Written).IsTrue();
        var length = output.Length;
        await Assert.That(async () => await PdfDocumentProtection.ProtectAsync(document, Restricted(), output, cancelled.Token)).Throws<OperationCanceledException>();

        await Assert.That(output.Length).IsEqualTo(length);
        await Assert.That(EncryptionTestFiles.ReadText(output.ToArray(), EncryptionTestFiles.UserPassword)).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>The owner changes the passwords: the old ones stop working and the new ones open the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerChangesThePasswords()
    {
        var protectedFile = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        using var document = PdfDocumentReader.Open(protectedFile, EncryptionTestFiles.OwnerPassword);
        await using var output = new MemoryStream();
        var changed = new PdfEncryptionOptions { UserPassword = NewUserPassword, OwnerPassword = NewOwnerPassword, Algorithm = PdfEncryptionAlgorithm.Aes128 };

        var result = PdfDocumentProtection.Protect(document, changed, output);

        await Assert.That(result.Written).IsTrue();
        var file = output.ToArray();
        await Assert.That(EncryptionTestFiles.ReadText(file, NewUserPassword)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.Inspect(file, NewOwnerPassword).Revision).IsEqualTo(StandardEncryptionWriter.Revision4);
        await Assert.That(() => StoreOpening.Open(file, EncryptionTestFiles.UserPassword)).Throws<PdfException>();
    }

    /// <summary>A user bound by permissions cannot change the protection without the owner password; the owner password lets them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UserNeedsTheOwnerPasswordToChangeProtection()
    {
        var protectedFile = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        using var document = PdfDocumentReader.Open(protectedFile, EncryptionTestFiles.UserPassword);
        var replacement = new PdfEncryptionOptions { UserPassword = NewUserPassword, OwnerPassword = NewOwnerPassword };

        await using var refused = new MemoryStream();
        var withoutPassword = PdfDocumentProtection.Protect(document, replacement, refused);
        await using var wrong = new MemoryStream();
        var withWrongPassword = PdfDocumentProtection.Protect(document, replacement, "not-the-owner", wrong);
        await using var allowed = new MemoryStream();
        var withOwnerPassword = PdfDocumentProtection.Protect(document, replacement, EncryptionTestFiles.OwnerPassword, allowed);

        await Assert.That(PdfDocumentProtection.GetProtection(document).CanChange).IsFalse();
        await Assert.That(withoutPassword.Refusal).Contains("owner password");
        await Assert.That(refused.Length).IsEqualTo(0);
        await Assert.That(withWrongPassword.Written).IsFalse();
        await Assert.That(wrong.Length).IsEqualTo(0);
        await Assert.That(withOwnerPassword.Written).IsTrue();
        await Assert.That(EncryptionTestFiles.ReadText(allowed.ToArray(), NewUserPassword)).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>The owner removes the protection: the copy has no /Encrypt, opens with no password and passes qpdf's check.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerRemovesTheProtection(CancellationToken cancellationToken)
    {
        var protectedFile = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        using var document = PdfDocumentReader.Open(protectedFile, EncryptionTestFiles.OwnerPassword);
        await using var output = new MemoryStream();

        var result = await PdfDocumentProtection.RemoveProtectionAsync(document, output, cancellationToken);

        await Assert.That(result.Written).IsTrue();
        var file = output.ToArray();
        using var store = StoreOpening.Open(file, null);
        await Assert.That(store.Security).IsNull();
        await Assert.That(store.Trailer.ContainsKey(KnownName.Encrypt)).IsFalse();
        await Assert.That(EncryptionTestFiles.ReadText(file, null)).Contains(EncryptionTestFiles.PageText);
        var check = await QpdfCommand.RunAsync("removed", file, ["--check", QpdfCommand.InputToken], cancellationToken);
        if (check is not null)
        {
            await Assert.That(check.Value.Output).Contains("No syntax or stream encoding errors found");
        }
    }

    /// <summary>A restricted user cannot remove the protection; with the owner password, or when nothing is restricted, they can.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingProtectionFollowsTheOwnerRules()
    {
        var restricted = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        var open = EncryptionTestFiles.Encrypt(
            EncryptionTestFiles.Plain(),
            new PdfEncryptionOptions { UserPassword = EncryptionTestFiles.UserPassword, OwnerPassword = EncryptionTestFiles.OwnerPassword },
            true);
        using var asUser = PdfDocumentReader.Open(restricted, EncryptionTestFiles.UserPassword);
        using var unrestricted = PdfDocumentReader.Open(open, EncryptionTestFiles.UserPassword);
        await using var refused = new MemoryStream();
        await using var withPassword = new MemoryStream();
        await using var anyone = new MemoryStream();

        var denied = PdfDocumentProtection.RemoveProtection(asUser, refused);
        var allowed = PdfDocumentProtection.RemoveProtection(asUser, EncryptionTestFiles.OwnerPassword, withPassword);
        var free = PdfDocumentProtection.RemoveProtection(unrestricted, anyone);

        await Assert.That(denied.Written).IsFalse();
        await Assert.That(refused.Length).IsEqualTo(0);
        await Assert.That(allowed.Written).IsTrue();
        await Assert.That(free.Written).IsTrue();
        await Assert.That(EncryptionTestFiles.ReadText(withPassword.ToArray(), null)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.ReadText(anyone.ToArray(), null)).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>A document that is not protected has nothing to remove, and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnprotectedDocumentHasNothingToRemove()
    {
        using var document = PdfDocumentReader.Open(EncryptionTestFiles.Plain(), null);
        await using var output = new MemoryStream();

        var result = PdfDocumentProtection.RemoveProtection(document, output);

        await Assert.That(result.Written).IsFalse();
        await Assert.That(result.Refusal).Contains("not protected");
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(PdfDocumentProtection.GetProtection(document).IsProtected).IsFalse();
    }

    /// <summary>The protection a document reports matches how it was made.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectionIsReported()
    {
        var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        using var document = PdfDocumentReader.Open(file, EncryptionTestFiles.UserPassword);

        var info = PdfDocumentProtection.GetProtection(document);

        await Assert.That(info.IsProtected).IsTrue();
        await Assert.That(info.IsPublicKey).IsFalse();
        await Assert.That(info.Algorithm).IsEqualTo(PdfEncryptionAlgorithm.Aes256);
        await Assert.That(info.Revision).IsEqualTo(StandardEncryptionWriter.Revision6);
        await Assert.That(info.Role).IsEqualTo(PdfSecurityRole.User);
        await Assert.That(info.Permissions & (PdfAccessPermissions.Print | PdfAccessPermissions.ModifyContent)).IsEqualTo(PdfAccessPermissions.Print);
    }

    /// <summary>Signed and PDF/A documents are refused with the reason and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedAndPdfADocumentsAreRefused()
    {
        var pdfa = ConformancePdf.Page();
        ConformancePdf.Claim(pdfa, PdfAPart, "B", null);
        using var signed = PdfDocumentReader.Open(Pdf20TestPdf.Permissions("UR3"), null);
        using var archive = PdfDocumentReader.Open(pdfa.ToBytes(), null);
        await using var first = new MemoryStream();
        await using var second = new MemoryStream();

        var signedResult = PdfDocumentProtection.Protect(signed, Restricted(), first);
        var archiveResult = PdfDocumentProtection.Protect(archive, Restricted(), second);

        await Assert.That(signedResult.Refusal).Contains("signed");
        await Assert.That(archiveResult.Refusal).Contains("PDF/A");
        await Assert.That(first.Length + second.Length).IsEqualTo(0);
    }

    /// <summary>An incremental save keeps a plain file plain and an encrypted file encrypted: it never adds encryption.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveNeverAddsEncryption(CancellationToken cancellationToken)
    {
        using var plain = PdfDocumentReader.Open(EncryptionTestFiles.Plain(), null);
        await using var plainOutput = new MemoryStream();
        var encrypted = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), true);
        using var protectedDocument = PdfDocumentReader.Open(encrypted, EncryptionTestFiles.OwnerPassword);
        await using var protectedOutput = new MemoryStream();

        await PdfDocumentSaving.SaveAsync(plain, plainOutput, cancellationToken);
        await PdfDocumentSaving.SaveAsync(protectedDocument, protectedOutput, cancellationToken);

        using var plainAgain = PdfDocumentReader.Open(plainOutput.ToArray(), null);
        using var protectedAgain = PdfDocumentReader.Open(protectedOutput.ToArray(), EncryptionTestFiles.UserPassword);
        await Assert.That(plainAgain.IsEncrypted).IsFalse();
        await Assert.That(protectedAgain.IsEncrypted).IsTrue();
    }

    /// <summary>Saving an encrypted file again with the compact writer keeps its passwords, with and without object streams.</summary>
    /// <param name="objectStreams">Whether the copy uses object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CompactSaveKeepsTheEncryption(bool objectStreams)
    {
        var protectedFile = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), Restricted(), objectStreams);
        using var store = StoreOpening.Open(protectedFile, EncryptionTestFiles.OwnerPassword);

        var again = PdfCompactWriter.Save(store, new(objectStreams, false));

        await Assert.That(EncryptionTestFiles.ReadText(again, EncryptionTestFiles.UserPassword)).Contains(EncryptionTestFiles.PageText);
        await Assert.That(EncryptionTestFiles.Inspect(again, EncryptionTestFiles.UserPassword).Role).IsEqualTo(PdfSecurityRole.User);
    }

    /// <summary>Gets options with both passwords, AES-256 and the permissions print and copy.</summary>
    /// <returns>The options.</returns>
    private static PdfEncryptionOptions Restricted() => new()
    {
        UserPassword = EncryptionTestFiles.UserPassword,
        OwnerPassword = EncryptionTestFiles.OwnerPassword,
        Permissions = PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent,
    };
}
