// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for signing with a certificate, and for remembering a certificate only when the user asks.</summary>
public sealed class CertificateSignTests
{
    /// <summary>The certificate file's password.</summary>
    private const string Password = "correct horse";

    /// <summary>The pages in the document.</summary>
    private const int Pages = 2;

    /// <summary>The shortest piece of a secret that must not appear in a saved file.</summary>
    private const int SecretSample = 24;

    /// <summary>The certificate file's name.</summary>
    private const string CertificateFile = "signer.pfx";

    /// <summary>A password that does not open the certificate.</summary>
    private const string WrongPassword = "wrong";

    /// <summary>The certificates remembered before one is forgotten.</summary>
    private const int RememberedCount = 2;

    /// <summary>Verifies signing writes a signed copy, does not remember the certificate unasked and opens the copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignsCopyAndOpensIt()
    {
        using var test = new TestServices();
        var certificatePath = WriteCertificate(test.Directory, CertificateFile);
        var destination = Path.Combine(test.Directory, "signed.pdf");
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("contract.pdf", Pages)]);
        var tab = main.SelectedTab!;
        string? suggested = null;
        using var sign = AnswerWith(tab, certificatePath, false);
        using var save = tab.SaveAsInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(destination);
        });

        _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        var opened = main.Tabs.FirstOrDefault(t => t.FilePath == destination);
        if (opened is not null)
        {
            await opened.EnsureLoadedAsync(CancellationToken.None);
        }

        opened?.Signatures.Refresh();

        await Assert.That(suggested).IsEqualTo("contract-signed.pdf");
        await Assert.That(tab.Notice).IsEqualTo("Signed copy saved as signed.pdf.");
        await Assert.That(test.Services.Settings.RememberedCertificates).IsEmpty();
        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Signatures.SignatureCount).IsEqualTo(1);
    }

    /// <summary>Verifies a wrong password is explained in the window instead of signing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsWrongPassword()
    {
        using var test = new TestServices();
        using var request = new CertificateSignViewModel([]) { CertificatePath = WriteCertificate(test.Directory, CertificateFile), Password = WrongPassword };

        _ = await request.SignCommand.Execute().ToTask();

        await Assert.That(request.Error).IsEqualTo("The password is not right, or the file is not a certificate.");
        await Assert.That(request.TakeCertificate()).IsNull();
    }

    /// <summary>The certificate is remembered only after the user ticks Remember, and is then offered first next time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemembersACertificateOnlyWhenChosen()
    {
        using var test = new TestServices();
        var certificatePath = WriteCertificate(test.Directory, CertificateFile);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("remember.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var save = tab.SaveAsInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, $"{Guid.NewGuid():N}.pdf")));

        using (AnswerWith(tab, certificatePath, false))
        {
            _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        }

        var unasked = test.Services.Settings.RememberedCertificates.Count;
        using (AnswerWith(tab, certificatePath, true))
        {
            _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        }

        var remembered = test.Services.Settings.RememberedCertificates.ToList();
        using var offered = new CertificateSignViewModel(remembered);

        await Assert.That(unasked).IsEqualTo(0);
        await Assert.That(remembered.Count).IsEqualTo(1);
        await Assert.That(remembered[0].Path).IsEqualTo(certificatePath);
        await Assert.That(remembered[0].Subject).IsEqualTo(TestSignedPdf.SignerName);
        await Assert.That(remembered[0].Thumbprint).IsNotEmpty();
        await Assert.That(offered.CertificatePath).IsEqualTo(certificatePath);
        await Assert.That(offered.Remember).IsTrue();
    }

    /// <summary>Each remembered certificate can be forgotten on its own; forgetting holds even when the window is cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForgetsEachRememberedCertificate()
    {
        using var test = new TestServices();
        var home = new RememberedCertificate(WriteCertificate(test.Directory, "home.pfx"), "Home", "AA");
        var work = new RememberedCertificate(WriteCertificate(test.Directory, "work.pfx"), "Work", "BB");
        test.Services.Settings.RememberedCertificates.AddRange([home, work]);
        test.Services.SaveSettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("forget.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var offered = 0;
        using var sign = tab.Signatures.CertificateSignInteraction.RegisterHandler(async context =>
        {
            offered = context.Input.Remembered.Count;
            context.Input.SelectedCertificate = work;
            _ = await context.Input.ForgetCommand.Execute().ToTask();
            context.SetOutput(false);
        });

        _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        var reopened = new SettingsStore(test.Services.SettingsStore.FilePath).Load();

        await Assert.That(offered).IsEqualTo(RememberedCount);
        await Assert.That(test.Services.Settings.RememberedCertificates).IsEquivalentTo([home]);
        await Assert.That(reopened.RememberedCertificates).IsEquivalentTo([home]);
    }

    /// <summary>
    /// After signing with a remembered certificate and remembering a signature, the saved files are reopened: the
    /// reuse choices are kept, and neither file holds the password, the certificate file or its private key.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedPreferencesHoldNoSecretsAfterReopening()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificateFile = certificate.Export(X509ContentType.Pkcs12, Password);
        var certificatePath = Path.Combine(test.Directory, CertificateFile);
        await File.WriteAllBytesAsync(certificatePath, certificateFile);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("secrets.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var save = tab.SaveAsInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "signed.pdf")));
        using (AnswerWith(tab, certificatePath, true))
        {
            _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        }

        using (tab.FillAndSign.MarkInteraction.RegisterHandler(static context =>
        {
            context.Input.Text = "Glenn Watson";
            context.Input.Remember = true;
            context.SetOutput(true);
        }))
        {
            _ = await tab.FillAndSign.SignatureCommand.Execute().ToTask();
        }

        _ = tab.FillAndSign.Cancel();
        test.Services.SaveSettings();

        RememberedCertificate[] remembered;
        string? signature;
        using (var reopened = new AppServices(new SettingsStore(test.Services.SettingsStore.FilePath), new PdfiumEngine(), new FallbackPlatform()))
        {
            remembered = [.. reopened.Settings.RememberedCertificates];
            signature = reopened.SignatureMarks.Signature?.Text;
        }

        var saved = await File.ReadAllTextAsync(test.Services.SettingsStore.FilePath) + await File.ReadAllTextAsync(test.Services.SignatureMarkStore.FilePath);
        var privateKey = Convert.ToBase64String(certificate.GetRSAPrivateKey()!.ExportPkcs8PrivateKey());
        var pfx = Convert.ToBase64String(certificateFile);

        await Assert.That(remembered.Select(static item => item.Path)).IsEquivalentTo([certificatePath]);
        await Assert.That(signature).IsEqualTo("Glenn Watson");
        await Assert.That(saved).DoesNotContain(Password);
        await Assert.That(saved.Contains("password", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(saved.Contains("privateKey", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(saved).DoesNotContain("PRIVATE KEY");
        await Assert.That(saved).DoesNotContain(privateKey[..SecretSample]);
        await Assert.That(saved).DoesNotContain(pfx[..SecretSample]);
        await Assert.That(saved).DoesNotContain(Convert.ToHexString(certificate.GetRSAPrivateKey()!.ExportPkcs8PrivateKey())[..SecretSample]);
    }

    /// <summary>
    /// The password request is labelled, masked and explained; the keyboard starts in it when a certificate is named,
    /// Enter signs, a wrong password puts the keyboard back in the box with the text selected, and Escape cancels.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordRequestIsClearAndKeyboardAccessible()
    {
        using var test = new TestServices();
        var path = WriteCertificate(test.Directory, CertificateFile);
        using var request = new CertificateSignViewModel([new(path, TestSignedPdf.SignerName, "AA")]);
        var window = new CertificateSignWindow { ViewModel = request };
        window.Show();
        try
        {
            var box = window.GetVisualDescendants().OfType<TextBox>().Single(static textBox => textBox.Name == "PasswordBox");
            var focused = await UiWait.UntilAsync(() => box.IsFocused);
            window.KeyTextInput(WrongPassword);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var explained = await UiWait.UntilAsync(() => request.Error is not null && box.IsFocused && box.SelectionEnd - box.SelectionStart == WrongPassword.Length);
            window.KeyTextInput(Password);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var signed = await UiWait.UntilAsync(() => !window.IsVisible);
            using var certificate = request.TakeCertificate();

            await Assert.That(focused).IsTrue();
            await Assert.That(box.PasswordChar).IsNotEqualTo(default(char));
            await Assert.That(AutomationProperties.GetName(box)).IsEqualTo("Certificate password");
            await Assert.That(AutomationProperties.GetHelpText(box)).Contains("never saved");
            await Assert.That(explained).IsTrue();
            await Assert.That(signed).IsTrue();
            await Assert.That(certificate).IsNotNull();
            await Assert.That(request.Password).IsEmpty();
        }
        finally
        {
            window.Close();
        }

        using var cancelled = new CertificateSignViewModel([]);
        var second = new CertificateSignWindow { ViewModel = cancelled };
        second.Show();
        try
        {
            var shown = await UiWait.UntilAsync(() => second.IsVisible);
            second.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            var closed = await UiWait.UntilAsync(() => !second.IsVisible);

            await Assert.That(shown).IsTrue();
            await Assert.That(closed).IsTrue();
            await Assert.That(cancelled.TakeCertificate()).IsNull();
        }
        finally
        {
            second.Close();
        }
    }

    /// <summary>Answers the certificate window by signing with a file, ticking Remember or not.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="certificatePath">The certificate file.</param>
    /// <param name="remember">Whether to remember the certificate.</param>
    /// <returns>The registration.</returns>
    private static IDisposable AnswerWith(DocumentTabViewModel tab, string certificatePath, bool remember) =>
        tab.Signatures.CertificateSignInteraction.RegisterHandler(async context =>
        {
            context.Input.CertificatePath = certificatePath;
            context.Input.Password = Password;
            context.Input.Reason = "Agreed";
            context.Input.Remember = remember;
            _ = await context.Input.SignCommand.Execute().ToTask();
            context.SetOutput(context.Input.Error is null);
        });

    /// <summary>Writes a test certificate with its key to a password protected .pfx file.</summary>
    /// <param name="directory">Where to write it.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The file.</returns>
    private static string WriteCertificate(string directory, string name)
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Password));
        return path;
    }
}
