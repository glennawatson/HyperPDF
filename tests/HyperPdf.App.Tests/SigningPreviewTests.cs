// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.App.Tests;

/// <summary>Tests for the preview the certificate signing window shows before a document is signed.</summary>
public sealed class SigningPreviewTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The index of the choice that certifies the document and allows no later change.</summary>
    private const int CertifyWithoutChanges = 1;

    /// <summary>A timestamp server address.</summary>
    private const string Server = "https://timestamp.example/tsa";

    /// <summary>The preview says where the signature goes, what kind it is, what is recorded and that no timestamp is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewDescribesWhatWillBeSigned()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("preview.pdf", Pages)]);
        var document = main.SelectedTab!.TryGetDocument();
        using var vm = new CertificateSignViewModel([], 0, r => PdfSigner.Preview(document, r), string.Empty);
        var plain = vm.PreviewText;
        vm.Reason = "Agreed";
        vm.Location = "Brisbane";
        vm.IsVisibleSignature = true;
        var detailed = vm.PreviewText;
        vm.CertificationIndex = CertifyWithoutChanges;
        var certified = vm.PreviewText;

        using (Assert.Multiple())
        {
            await Assert.That(vm.HasPreview).IsTrue();
            await Assert.That(plain).Contains("on page 1, not drawn on the page.");
            await Assert.That(plain).Contains("Signature type: standard (PKCS #7), digest SHA-256.");
            await Assert.That(plain).Contains("A new copy is saved; this file is not changed.");
            await Assert.That(plain).Contains("No timestamp is added.");
            await Assert.That(detailed).Contains("Reason: Agreed");
            await Assert.That(detailed).Contains("Location: Brisbane");
            await Assert.That(detailed).Contains("on page 1, drawn on the page.");
            await Assert.That(certified).IsNotEqualTo(detailed);
        }
    }

    /// <summary>A timestamp server from Preferences is named in the preview.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewNamesTheTimestampServer()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("stamp.pdf", Pages)]);
        var document = main.SelectedTab!.TryGetDocument();
        using var vm = new CertificateSignViewModel([], 0, r => PdfSigner.Preview(document, r), Server);

        await Assert.That(vm.PreviewText).Contains($"A trusted timestamp is added from {Server}.");
    }

    /// <summary>The window shows the preview and keeps it current as choices change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowShowsThePreview()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("window.pdf", Pages)]);
        var document = main.SelectedTab!.TryGetDocument();
        using var vm = new CertificateSignViewModel([], 0, r => PdfSigner.Preview(document, r), string.Empty);
        var window = new CertificateSignWindow { ViewModel = vm };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var text = window.FindControl<TextBlock>("PreviewText")!;
            vm.Reason = "Reviewed";
            var updated = await UiWait.UntilAsync(() => text.Text?.Contains("Reason: Reviewed", StringComparison.Ordinal) == true);

            await Assert.That(updated).IsTrue();
            await Assert.That(window.FindControl<StackPanel>("PreviewPanel")!.IsVisible).IsTrue();
            await Assert.That(text.Text).IsEqualTo(vm.PreviewText);
        }
        finally
        {
            window.Close();
        }
    }
}
