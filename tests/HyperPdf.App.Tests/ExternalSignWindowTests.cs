// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using Avalonia.Automation;
using Avalonia.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Drives the external signing window: the steps turn on in order, nothing moves, and every control is named.</summary>
public sealed class ExternalSignWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 680;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 860;

    /// <summary>The name of the button that chooses the certificates.</summary>
    private const string BrowseButtonName = "BrowseButton";

    /// <summary>Prepare turns on once the certificates are read, and Save and Finish once the file is prepared, without moving the sections.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StepsTurnOnInOrderAndTheLayoutStaysStill()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, "signer.cer");
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("minutes.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var vm = new ExternalSignViewModel(DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument())!, tab.FilePath, 0, null, null);
        var window = new ExternalSignWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
            var prepare = window.FindControl<Button>("PrepareButton")!;
            var saveToSign = window.FindControl<Button>("SaveToSignButton")!;
            var finish = window.FindControl<Button>("FinishButton")!;
            var pending = window.FindControl<SelectableTextBlock>("PendingText")!;
            var certificates = window.FindControl<TextBox>("CertificateBox")!;
            var idleBounds = new[] { prepare.Bounds, saveToSign.Bounds, finish.Bounds, pending.Bounds };

            await Assert.That(prepare.Command!.CanExecute(null)).IsFalse();
            await Assert.That(saveToSign.Command!.CanExecute(null)).IsFalse();
            await Assert.That(finish.Command!.CanExecute(null)).IsFalse();
            await Assert.That(window.FindControl<ComboBox>("KeyBox")!.ItemCount).IsEqualTo(vm.KeyChoices.Count);
            await Assert.That(window.FindControl<ComboBox>("ResultBox")!.ItemCount).IsEqualTo(vm.ResultChoices.Count);

            window.FindControl<Button>(BrowseButtonName)!.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.HasSigner && certificates.Text?.Length > 0);

            await Assert.That(certificates.Text).IsEqualTo($"{TestSignedPdf.SignerName}: 1 certificate read.");
            await Assert.That(prepare.Command.CanExecute(null)).IsTrue();
            await Assert.That(saveToSign.Command.CanExecute(null)).IsFalse();

            prepare.Command.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.HasPending && pending.Text?.Length > 0);

            await Assert.That(pending.Text).Contains("Bytes to sign:");
            await Assert.That(saveToSign.Command.CanExecute(null)).IsTrue();
            await Assert.That(finish.Command.CanExecute(null)).IsTrue();
            await Assert.That(new[] { prepare.Bounds, saveToSign.Bounds, finish.Bounds, pending.Bounds }).IsEquivalentTo(idleBounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control has a name, the close button cancels, and the status lines are announced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsAreNamed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("named.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var vm = new ExternalSignViewModel(DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument())!, tab.FilePath, 0, null, null);
        var window = new ExternalSignWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);

            var focused = await UiWait.UntilAsync(() => window.FindControl<Button>(BrowseButtonName)!.IsFocused);

            await Assert.That(focused).IsTrue();
            foreach (var name in new[] { "CertificateBox", BrowseButtonName, "KeyBox", "ReasonBox", "LocationBox", "ResultBox", "CopyProgress", "ResultText", "PendingText" })
            {
                await Assert.That(AutomationProperties.GetName(window.FindControl<Control>(name)!)).IsNotEmpty().Because(name);
            }

            await Assert.That(window.FindControl<Button>("CloseButton")!.IsCancel).IsTrue();
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("StatusText")!)).IsEqualTo(AutomationLiveSetting.Polite);
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("ErrorText")!)).IsEqualTo(AutomationLiveSetting.Assertive);
        }
        finally
        {
            window.Close();
        }
    }
}
