// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>
/// Checks that the heavy library calls run on the pool, because the library does its work before its first await and
/// would otherwise freeze the window and leave Cancel unable to respond.
/// </summary>
public sealed class OffUiThreadWorkTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The operations a rotate and an undo make.</summary>
    private const int EditCalls = 2;

    /// <summary>What the external signer answers; the fake ignores it.</summary>
    private static readonly byte[] SignerAnswer = [1];

    /// <summary>A redaction runs off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactionRunsOffTheUiThread()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor();
        using var vm = new RedactionViewModel(fake, test.CreateDocument("redact.pdf", Pages), 1, 1, false);
        vm.Understands = true;
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "out.pdf")));
        var uiThread = Environment.CurrentManagedThreadId;

        _ = await vm.ApplyCommand.Execute().ToTask();

        await Assert.That(fake.ThreadId).IsNotEqualTo(uiThread);
    }

    /// <summary>Finishing an external signature runs off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishingAnExternalSignatureRunsOffTheUiThread()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, "signer.cer");
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        var answer = Path.Combine(test.Directory, "answer.bin");
        await File.WriteAllBytesAsync(answer, SignerAnswer);
        var fake = new FakeDocumentSigner { Pending = new FakePendingSignature() };
        using var vm = new ExternalSignViewModel(fake, test.CreateDocument("sign.pdf", Pages), 0, null, null);
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
        using var open = vm.OpenSignatureInteraction.RegisterHandler(context => context.SetOutput(answer));
        using var copy = vm.SaveCopyInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "signed.pdf")));
        _ = await vm.BrowseCommand.Execute().ToTask();
        _ = await vm.PrepareCommand.Execute().ToTask();
        var uiThread = Environment.CurrentManagedThreadId;

        _ = await vm.FinishCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).Contains(nameof(IDocumentSigner.CompleteAsync));
            await Assert.That(fake.ThreadId).IsNotEqualTo(uiThread);
        }
    }

    /// <summary>Saving a document serializes it off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingADocumentRunsOffTheUiThread()
    {
        using var test = new TestServices();
        var path = test.CreateDocument("save.pdf", Pages);
        var source = test.Services.Pool.Create(path, null);
        var editor = new FakeAnnotationEditor();
        var uiThread = Environment.CurrentManagedThreadId;

        var outcome = await DocumentSave.SaveAsync(editor, source, Path.Combine(test.Directory, "saved.pdf"), CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(outcome.Saved).IsTrue();
            await Assert.That(editor.ThreadId).IsNotEqualTo(uiThread);
        }
    }

    /// <summary>Page edits and undo run off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageEditsRunOffTheUiThread()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var pages = tab.Pages;
        pages.SelectPages([1]);
        var uiThread = Environment.CurrentManagedThreadId;

        _ = await pages.RotateClockwiseCommand.Execute().ToTask();
        _ = await pages.UndoCommand.Execute().ToTask();

        var calls = engine.Calls.ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(calls.Length).IsGreaterThanOrEqualTo(EditCalls);
            await Assert.That(Array.TrueForAll(calls, call => call.ThreadId != uiThread)).IsTrue();
        }
    }

    /// <summary>Saving an attachment extracts it off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingAnAttachmentRunsOffTheUiThread()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        var path = test.CreateDocument("attached.pdf", TestPdf.CreateWithAttachment());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var attachments = main.SelectedTab!.Attachments;
        using var handler = attachments.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "notes.txt")));
        attachments.Selected = attachments.Items[0];
        var uiThread = Environment.CurrentManagedThreadId;

        _ = await attachments.SaveCommand.Execute().ToTask();

        var calls = engine.Calls.ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(calls.Length).IsEqualTo(1);
            await Assert.That(calls[0].ThreadId).IsNotEqualTo(uiThread);
        }
    }

    /// <summary>Preparing a document for the system print dialog exports its pages off the UI thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportForPrintingRunsOffTheUiThread()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()), engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("printing.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var uiThread = Environment.CurrentManagedThreadId;

        _ = await tab.Printing.PrintWithSystemDialogCommand.Execute().ToTask();

        var calls = engine.Calls.ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(calls.Length).IsEqualTo(1);
            await Assert.That(calls[0].ThreadId).IsNotEqualTo(uiThread);
        }
    }
}
