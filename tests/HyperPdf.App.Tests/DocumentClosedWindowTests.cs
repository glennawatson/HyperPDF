// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>
/// Checks that a copy or sign window whose document was closed or reloaded under it says so in its own error line,
/// and that the tab closes the window rather than leave it holding a document that is gone.
/// </summary>
public sealed class DocumentClosedWindowTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "closed.pdf";

    /// <summary>The object name the closed document's exception carries.</summary>
    private const string DisposedName = "document";

    /// <summary>The name of the copy the windows are asked to write.</summary>
    private const string OutputName = "out.pdf";

    /// <summary>The start of the error line a copy window shows.</summary>
    private const string CopyError = "Could not save the copy: ";

    /// <summary>A flatten window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlattenWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new FlattenCopyViewModel(new FakeFlattenCopyWriter { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages));
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>A PDF/A window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArchiveWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new ArchiveCopyViewModel(new FakeArchiveCopyWriter { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages));
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>A PDF 2.0 window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20WindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new Pdf20CopyViewModel(new FakePdf20CopyWriter { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages));
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>An optimise window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimizeWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new OptimizeCopyViewModel(new FakeOptimizer { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages), "en-US");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>A protect window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new ProtectViewModel(new FakeDocumentProtector { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages));
        vm.UserPassword = "reading";
        vm.UserPasswordRepeat = "reading";
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>A remove protection window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemoveProtectionWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new RemoveProtectionViewModel(new FakeDocumentProtector { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages));
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>A redaction window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactionWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var vm = new RedactionViewModel(new FakeRedactor { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages), 1, 1, false);
        vm.Understands = true;
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.ApplyCommand.Execute().ToTask();

        await Assert.That(vm.Error).Contains(CopyFailure.DocumentClosedText);
        await Assert.That(vm.IsRunning).IsFalse();
    }

    /// <summary>A validation data window shows a closed document in its error line and does not let the exception escape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        var signer = new FakeDocumentSigner { Failure = new ObjectDisposedException(DisposedName) };
        using var vm = new ValidationDataViewModel(signer, test.CreateDocument(SourceName, Pages), new NoRevocationSource(), null);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, OutputName)));

        _ = await vm.SaveCommand.Execute().ToTask();

        await AssertClosedAsync(vm.Error, vm.IsRunning);
    }

    /// <summary>An external signing window shows a closed document in its error line when preparing fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExternalSignWindowExplainsAClosedDocument()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var certificatePath = Path.Combine(test.Directory, "signer.cer");
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Cert));
        using var vm = new ExternalSignViewModel(new FakeDocumentSigner { Failure = new ObjectDisposedException(DisposedName) }, test.CreateDocument(SourceName, Pages), 0, null, null);
        using var browse = vm.BrowseInteraction.RegisterHandler(context => context.SetOutput([certificatePath]));
        _ = await vm.BrowseCommand.Execute().ToTask();

        _ = await vm.PrepareCommand.Execute().ToTask();

        await Assert.That(vm.Error).IsEqualTo($"Could not prepare the signature: {CopyFailure.DocumentClosedText}");
        await Assert.That(vm.IsRunning).IsFalse();
    }

    /// <summary>A reload of the file closes the copy window the tab opened, instead of leaving it on a document that is gone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReloadClosesTheOpenCopyWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        var shown = new TaskCompletionSource<FlattenCopyViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = tab.Copies.Flatten.Interaction.RegisterHandler(async context =>
        {
            // Stands in for the window: it stays up until the view model's close command runs.
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var closing = context.Input.CloseCommand.SubscribeSafe(_ => closed.TrySetResult(), static _ => { });
            _ = shown.TrySetResult(context.Input);
            await closed.Task;
            context.SetOutput(RxVoid.Default);
        });

        var running = tab.Copies.Flatten.RunCommand.Execute().ToTask();
        _ = await shown.Task;
        await tab.ReloadAsync();
        _ = await running;

        await Assert.That(tab.Notice).Contains("window for saving a copy was closed");
    }

    /// <summary>Closing the tab closes the copy window the tab opened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingTheTabClosesTheOpenCopyWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        var shown = new TaskCompletionSource<FlattenCopyViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = tab.Copies.Flatten.Interaction.RegisterHandler(async context =>
        {
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var closing = context.Input.CloseCommand.SubscribeSafe(_ => closed.TrySetResult(), static _ => { });
            _ = shown.TrySetResult(context.Input);
            await closed.Task;
            context.SetOutput(RxVoid.Default);
        });

        var running = tab.Copies.Flatten.RunCommand.Execute().ToTask();
        _ = await shown.Task;
        main.CloseTabWithoutAsking(tab);

        _ = await running;
    }

    /// <summary>Failures no command handled reach the reader as a notice on the selected tab in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnhandledCommandFailureBecomesANotice()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        using var attached = CommandFailureObserver.Instance.Attach(main.ReportFailure);

        CommandFailureObserver.Instance.OnNext(new ObjectDisposedException(DisposedName));

        await Assert.That(main.SelectedTab!.Notice).IsEqualTo("That action stopped because the document was closed or reloaded.");

        CommandFailureObserver.Instance.OnNext(new FormatException("The value is not a number."));

        await Assert.That(main.SelectedTab.Notice).IsEqualTo("That action did not finish: The value is not a number.");
    }

    /// <summary>A page edit that arrives after the tab was closed, such as the last page of a text recognition run, is ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageEditAfterTheTabClosedIsIgnored()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        main.CloseTabWithoutAsking(tab);

        tab.OnPageEdited(0);
        tab.OnLayersChanged();

        await Assert.That(tab.LifetimeToken.IsCancellationRequested).IsTrue();
    }

    /// <summary>Checks a window's error line names the closed document.</summary>
    /// <param name="error">The error line.</param>
    /// <param name="isRunning">Whether the window still says it is running.</param>
    /// <returns>A task.</returns>
    private static async Task AssertClosedAsync(string? error, bool isRunning)
    {
        using (Assert.Multiple())
        {
            await Assert.That(error).IsEqualTo($"{CopyError}{CopyFailure.DocumentClosedText}");
            await Assert.That(isRunning).IsFalse();
        }
    }
}
