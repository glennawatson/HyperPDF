// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Signatures;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Signing;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests for <see cref="SignaturesViewModel"/> and low-dialog signing workflows.</summary>
public sealed class SignatureViewModelTests
{
    /// <summary>The page count of the signed document.</summary>
    private const int Pages = 2;

    /// <summary>The password protecting the test PKCS #12 file.</summary>
    private const string CertificatePassword = "test-password";

    /// <summary>Verifies a signed document reports its signature, and checking shows it with its signer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChecksSignaturesWhenAsked()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Path.Combine(test.Directory, "signed.pdf");
        await File.WriteAllBytesAsync(path, TestSignedPdf.Create(Pages, certificate));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var signatures = main.SelectedTab!.Signatures;
        var shown = 0;
        using var show = signatures.ShowInteraction.RegisterHandler(context =>
        {
            shown++;
            context.SetOutput(RxVoid.Default);
        });

        var before = signatures.Signatures.Count;
        _ = await signatures.CheckCommand.Execute().ToTask();

        await Assert.That(signatures.SignatureCount).IsEqualTo(1);
        await Assert.That(before).IsEqualTo(0);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(signatures.Signatures[0].SignerName).IsEqualTo(TestSignedPdf.SignerName);
        await Assert.That(signatures.Signatures[0].Integrity).IsEqualTo(SignatureIntegrity.Intact);
    }

    /// <summary>Verifies inline checking populates the signed-document summary without opening the modal signatures dialog.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CheckInline_SignedDocument_UpdatesStatusSummaryWithoutModalDialog()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Path.Combine(test.Directory, "inline.pdf");
        await File.WriteAllBytesAsync(path, TestSignedPdf.Create(Pages, certificate));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var countBeforeMakingPart = tab.SignatureCount;
        var unmadeAtOpen = tab.SignaturesIfMade is null;

        var signatures = tab.Signatures;
        var shown = 0;
        using var show = signatures.ShowInteraction.RegisterHandler(context =>
        {
            shown++;
            context.SetOutput(RxVoid.Default);
        });

        _ = await signatures.CheckInlineCommand.Execute().ToTask();

        await Assert.That(countBeforeMakingPart).IsEqualTo(1);
        await Assert.That(unmadeAtOpen).IsTrue();
        await Assert.That(shown).IsEqualTo(0);
        await Assert.That(signatures.StatusSummary).Contains(TestSignedPdf.SignerName);
    }

    /// <summary>Verifies certificate signing uses the pre-populated destination path inside the dialog without popping a second Save As dialog.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SignWithCertificate_SuggestedDestination_WritesSignedFileWithoutSecondSavePrompt()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var pdfPath = Path.Combine(test.Directory, "contract.pdf");
        var pfxPath = Path.Combine(test.Directory, "signer.pfx");
        await File.WriteAllBytesAsync(pdfPath, TestPdf.Create(Pages));
        await File.WriteAllBytesAsync(pfxPath, certificate.Export(X509ContentType.Pfx, CertificatePassword));

        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [pdfPath]);
        var tab = main.SelectedTab!;
        var signatures = tab.Signatures;
        var savePrompts = 0;
        var chosenDestination = string.Empty;

        using var saveAs = tab.SaveAsInteraction.RegisterHandler(context =>
        {
            savePrompts++;
            context.SetOutput(null);
        });
        using var sign = signatures.CertificateSignInteraction.RegisterHandler(context =>
        {
            chosenDestination = context.Input.DestinationPath;
            context.Input.CertificatePath = pfxPath;
            context.Input.Password = CertificatePassword;
            return context.Input.SignCommand.Execute().Do(context.SetOutput);
        });

        _ = await signatures.SignWithCertificateCommand.Execute().ToTask();

        await Assert.That(savePrompts).IsEqualTo(0);
        await Assert.That(chosenDestination).EndsWith("contract-signed.pdf");
        await Assert.That(File.Exists(chosenDestination)).IsTrue();
    }

    /// <summary>Verifies a remembered hand signature starts placement straight away without opening the mark editor unless Change Signature is pressed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SignatureCommand_RememberedMark_StartsPlacementWithoutOpeningMarkDialog()
    {
        using var test = new TestServices();
        test.Services.SignatureMarks.Set(SignatureMarkKind.Signature, SignatureMark.Typed(SignatureMarkKind.Signature, "Ada Lovelace"));
        var path = Path.Combine(test.Directory, "plain.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages));

        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var fill = main.SelectedTab!.FillAndSign;
        var markDialogs = 0;
        using var mark = fill.MarkInteraction.RegisterHandler(context =>
        {
            markDialogs++;
            context.SetOutput(false);
        });

        _ = await fill.SignatureCommand.Execute().ToTask();
        var placedWithoutDialog = fill.Placement is not null && markDialogs == 0;

        _ = await fill.EditSignatureCommand.Execute().ToTask();

        await Assert.That(fill.HasSavedSignature).IsTrue();
        await Assert.That(placedWithoutDialog).IsTrue();
        await Assert.That(markDialogs).IsEqualTo(1);
    }
}
