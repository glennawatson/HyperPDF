// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Tests for <see cref="ExternalSignViewModel"/>: a signer whose key stays outside the app signs a copy in three steps.</summary>
public sealed class ExternalSignViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "contract.pdf";

    /// <summary>The name of the signer's certificate file.</summary>
    private const string CertificateFile = "signer.cer";

    /// <summary>The name of the file the signer's answer is in.</summary>
    private const string AnswerFile = "answer.bin";

    /// <summary>The days a test certificate stays valid.</summary>
    private const int ValidDays = 30;

    /// <summary>The first serial number of a test certificate.</summary>
    private const int Serial = 7;

    /// <summary>The certificates in a chain of a leaf and its root.</summary>
    private const int ChainLength = 2;

    /// <summary>The tab command is on for an engine that signs, and the three steps produce a signed copy that opens.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignsACopyInThreeSteps()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, CertificateFile);
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        using var vm = Create(tab, null);
        var toSign = Path.Combine(test.Directory, "to-sign.bin");
        var answer = Path.Combine(test.Directory, AnswerFile);
        var destination = Path.Combine(test.Directory, "signed.pdf");
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
        using var save = vm.SaveToSignInteraction.RegisterHandler(context => context.SetOutput(toSign));
        using var open = vm.OpenSignatureInteraction.RegisterHandler(context => context.SetOutput(answer));
        string? suggested = null;
        using var copy = vm.SaveCopyInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(destination);
        });

        _ = await vm.BrowseCommand.Execute().ToTask();
        var readText = vm.SignerText;
        vm.Reason = "Agreed";
        _ = await vm.PrepareCommand.Execute().ToTask();
        var prepared = vm.PendingText;
        _ = await vm.SaveToSignCommand.Execute().ToTask();

        // The key stays with the signer: only the saved bytes go in, and only the value comes back.
        using var key = certificate.GetRSAPrivateKey()!;
        var value = key.SignData(await File.ReadAllBytesAsync(toSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        await File.WriteAllBytesAsync(answer, value);
        _ = await vm.FinishCommand.Execute().ToTask();
        main.Open([destination]);
        var opened = main.Tabs.First(t => t.FilePath == destination);
        await opened.EnsureLoadedAsync(CancellationToken.None);
        opened.Signatures.Refresh();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Copies.ExternalSign.CanRun).IsTrue();
            await Assert.That(readText).IsEqualTo($"{TestSignedPdf.SignerName}: 1 certificate read.");
            await Assert.That(prepared).Contains("Document digest");
            await Assert.That(prepared).Contains("Bytes to sign:");
            await Assert.That(new FileInfo(toSign).Length).IsGreaterThan(0);
            await Assert.That(suggested).IsEqualTo("contract-signed.pdf");
            await Assert.That(vm.Error).IsNull();
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
            await Assert.That(vm.StatusText).IsEqualTo("Done. The signed copy is saved.");
            await Assert.That(vm.ResultText).Contains("Saved as signed.pdf.");
            await Assert.That(vm.HasPending).IsFalse();
            await Assert.That(opened.Signatures.SignatureCount).IsEqualTo(1);
        }
    }

    /// <summary>The signed copy is opened in the viewer, and a PEM file works as well as a DER one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensTheSignedCopy()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, "signer.pem");
        await File.WriteAllTextAsync(certificatePath, certificate.ExportCertificatePem());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var requested = new List<string>();
        using var vm = Create(main.SelectedTab!, requested.Add);
        var toSign = Path.Combine(test.Directory, "to-sign.bin");
        var answer = Path.Combine(test.Directory, AnswerFile);
        var destination = Path.Combine(test.Directory, "signed.pdf");
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
        using var save = vm.SaveToSignInteraction.RegisterHandler(context => context.SetOutput(toSign));
        using var open = vm.OpenSignatureInteraction.RegisterHandler(context => context.SetOutput(answer));
        using var copy = vm.SaveCopyInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.BrowseCommand.Execute().ToTask();
        _ = await vm.PrepareCommand.Execute().ToTask();
        _ = await vm.SaveToSignCommand.Execute().ToTask();
        using var key = certificate.GetRSAPrivateKey()!;
        await File.WriteAllBytesAsync(answer, key.SignData(await File.ReadAllBytesAsync(toSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        _ = await vm.FinishCommand.Execute().ToTask();

        await Assert.That(requested).IsEquivalentTo([destination]);
    }

    /// <summary>An answer that is not a signature for the prepared bytes is explained and saves nothing.</summary>
    /// <param name="container">Whether the answer is given as a whole signature container.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AWrongAnswerIsExplainedAndSavesNothing(bool container)
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, CertificateFile);
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        var answer = Path.Combine(test.Directory, AnswerFile);
        await File.WriteAllBytesAsync(answer, new byte[Pages * Pages]);
        var destination = Path.Combine(test.Directory, "never.pdf");
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        using var vm = Create(main.SelectedTab!, null);
        vm.SelectedResult = vm.ResultChoices[container ? 1 : 0];
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
        using var open = vm.OpenSignatureInteraction.RegisterHandler(context => context.SetOutput(answer));
        using var copy = vm.SaveCopyInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.BrowseCommand.Execute().ToTask();
        _ = await vm.PrepareCommand.Execute().ToTask();
        _ = await vm.FinishCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).StartsWith("Could not finish the signature:");
            await Assert.That(vm.StatusText).IsEqualTo("The copy was not saved.");
            await Assert.That(vm.IsRunning).IsFalse();
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists($"{destination}.signing")).IsFalse();
        }
    }

    /// <summary>Changing a choice after Prepare discards the prepared file, because it was made with the old choice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingAChoiceDiscardsThePreparedFile()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, CertificateFile);
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        using var vm = Create(main.SelectedTab!, null);
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));

        _ = await vm.BrowseCommand.Execute().ToTask();
        _ = await vm.PrepareCommand.Execute().ToTask();
        var prepared = vm.HasPending;
        vm.Location = "Brisbane";

        using (Assert.Multiple())
        {
            await Assert.That(prepared).IsTrue();
            await Assert.That(vm.HasPending).IsFalse();
            await Assert.That(vm.PendingText).IsEmpty();
        }
    }

    /// <summary>A file that holds no certificate is explained and leaves the window unready.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnUnreadableCertificateFileIsExplained()
    {
        using var test = new TestServices();
        var bad = Path.Combine(test.Directory, "bad.cer");
        await File.WriteAllTextAsync(bad, "this is not a certificate");
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        using var vm = Create(main.SelectedTab!, null);
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([bad]));

        _ = await vm.BrowseCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).StartsWith("Could not read the certificates:");
            await Assert.That(vm.HasSigner).IsFalse();
            await Assert.That(vm.SignerText).IsEmpty();
        }
    }

    /// <summary>A bundle in any order gives the signer's own certificate first, then its issuer, and picks the key kind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ABundleIsPutInChainOrder()
    {
        using var test = new TestServices();
        var now = TimeProvider.System.GetUtcNow();
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rootRequest = new CertificateRequest("CN=Chain Root", rootKey, HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(ValidDays));
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest("CN=Chain Leaf", leafKey, HashAlgorithmName.SHA256);
        using var leaf = leafRequest.Create(root, now.AddDays(-1), now.AddDays(ValidDays), BitConverter.GetBytes(Serial));
        var bundle = Path.Combine(test.Directory, "bundle.pem");

        // The root is written first, so the order in the file is not the order of the chain.
        await File.WriteAllTextAsync(bundle, $"{root.ExportCertificatePem()}\n{leaf.ExportCertificatePem()}");

        var loaded = SignerCertificateFiles.Load([bundle]);

        using (Assert.Multiple())
        {
            await Assert.That(loaded.Subject).IsEqualTo("Chain Leaf");
            await Assert.That(loaded.Identity.Certificates.Count).IsEqualTo(ChainLength);
            await Assert.That(loaded.Identity.Certificates[0]).IsEquivalentTo(leaf.RawData);
            await Assert.That(loaded.Identity.Certificates[1]).IsEquivalentTo(root.RawData);
            await Assert.That(loaded.Identity.KeyKind).IsEqualTo(SigningKeyKind.Ecdsa);
        }
    }

    /// <summary>Makes the window's view model over the tab's open document.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="openCopy">Opens a saved copy, or <see langword="null"/>.</param>
    /// <returns>The view model.</returns>
    private static ExternalSignViewModel Create(DocumentTabViewModel tab, Action<string>? openCopy) =>
        new(DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument())!, tab.FilePath, 0, null, openCopy);
}
