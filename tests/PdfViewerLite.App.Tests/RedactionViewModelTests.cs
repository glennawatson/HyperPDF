// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Redaction;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="RedactionViewModel"/> and the tab command that opens it, with a fake redactor and with a real document.</summary>
public sealed class RedactionViewModelTests
{
    /// <summary>The areas the fake counts as marked.</summary>
    private const int Areas = 3;

    /// <summary>The pages the fake counts as marked.</summary>
    private const int MarkedPages = 2;

    /// <summary>The name of the source file.</summary>
    private const string SourceName = "contract.pdf";

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".redacting";

    /// <summary>The page edge used to mark the whole page.</summary>
    private const float PageEdge = 700;

    /// <summary>The settings the choices in the test describe.</summary>
    private static readonly RedactionSettings Expected = new(RedactionImageChoice.Remove, RedactionLineArtChoice.RemoveTouched, false, true, true);

    /// <summary>The corner the whole-page mark starts at.</summary>
    private static readonly PagePoint Origin = new(0, 0);

    /// <summary>The corner the whole-page mark ends at.</summary>
    private static readonly PagePoint Far = new(PageEdge, PageEdge);

    /// <summary>The apply command stays off until the person confirms they understand the removal cannot be undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ApplyNeedsTheConfirmation()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor();
        using var vm = Create(test, fake, Areas);
        var command = (ICommand)vm.ApplyCommand;

        await Assert.That(command.CanExecute(null)).IsFalse();
        vm.Understands = true;
        await Assert.That(command.CanExecute(null)).IsTrue();
        await Assert.That(vm.WarningText).Contains("cannot be brought back");
    }

    /// <summary>The choices reach the redactor, the copy is written and the summary says what went.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoicesReachTheRedactorAndTheSummaryReportsThem()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor();
        using var vm = Create(test, fake, Areas);
        var destination = Path.Combine(test.Directory, "redacted.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
        vm.Understands = true;
        vm.SelectedImage = vm.ImageOptions[2];
        vm.SelectedLineArt = vm.LineArtOptions[2];
        vm.RemoveHiddenText = false;
        vm.ScrubMetadata = true;

        _ = await vm.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Received).IsEqualTo(Expected);
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
            await Assert.That(vm.ResultText).Contains("120 text characters");
            await Assert.That(vm.ResultText).Contains("Your open file was not changed");
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
        }
    }

    /// <summary>The open file is never overwritten.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor();
        using var vm = Create(test, fake, Areas);
        var source = Path.Combine(test.Directory, SourceName);
        var before = await File.ReadAllBytesAsync(source);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(source));
        vm.Understands = true;

        _ = await vm.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).IsEqualTo(0);
            await Assert.That(vm.Error).Contains("replace the file you have open");
            await Assert.That(await File.ReadAllBytesAsync(source)).IsEquivalentTo(before);
        }
    }

    /// <summary>A failed run leaves no file and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureLeavesNoFile()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor { Failure = new InvalidDataException("The file is damaged.") };
        using var vm = Create(test, fake, Areas);
        var destination = Path.Combine(test.Directory, "failed.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
        vm.Understands = true;

        _ = await vm.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
            await Assert.That(vm.Error).Contains("The file is damaged.");
            await Assert.That(vm.IsRunning).IsFalse();
        }
    }

    /// <summary>With nothing marked, applying says so and writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingMarkedWritesNothing()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor();
        using var vm = Create(test, fake, 0);
        vm.Understands = true;

        _ = await vm.ApplyCommand.Execute().ToTask();

        await Assert.That(fake.Calls).IsEqualTo(0);
        await Assert.That(vm.Summary).Contains("Nothing is marked");
        await Assert.That(vm.Error).Contains("Nothing is marked");
    }

    /// <summary>The command is available with HyperPDF and explains its limit with the PDFium reference engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommandFollowsTheEngine()
    {
        using var pdfium = new TestServices(TestEngineChoice.Pdfium);
        using var pdfiumMain = new MainViewModel(pdfium.Services);
        pdfiumMain.Open([pdfium.CreateDocument(SourceName, 1)]);
        using var native = new TestServices(TestEngineChoice.HyperPdf);
        using var nativeMain = new MainViewModel(native.Services);
        nativeMain.Open([native.CreateDocument(SourceName, 1)]);
        await Assert.That(await UiWait.UntilAsync(() => nativeMain.SelectedTab!.IsLoaded)).IsTrue();

        using (Assert.Multiple())
        {
            await Assert.That(pdfiumMain.SelectedTab!.CanRedact).IsFalse();
            await Assert.That(pdfiumMain.SelectedTab.RedactMenuText).Contains("needs the HyperPDF engine");
            await Assert.That(nativeMain.SelectedTab!.CanRedact).IsTrue();
        }
    }

    /// <summary>
    /// On HyperPDF: mark the whole page, apply and save a redacted copy through the window's view model, then reopen the
    /// copy. The copy has no text, the open file still has its text, and the marks are still on the open document.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkApplySaveAndReopen()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, 1)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var destination = Path.Combine(test.Directory, "redacted.pdf");
        var marked = tab.Annotations.AddShape(0, AnnotationKind.Redaction, Origin, Far);
        var before = tab.TryGetDocument()!.GetCharacterCount(0);
        RedactionReport? report = null;
        using var handler = tab.RedactInteraction.RegisterHandler(async context =>
        {
            using var picker = context.Input.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            context.Input.Understands = true;
            _ = await context.Input.ApplyCommand.Execute().ToTask();
            report = context.Input.Report;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.ApplyRedactionsCommand.Execute().ToTask();

        await TestServices.OpenAndWaitAsync(main, [destination]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        var copy = main.Tabs.Single(t => t.FilePath == destination).TryGetDocument()!;
        using (Assert.Multiple())
        {
            await Assert.That(marked).IsTrue();
            await Assert.That(before).IsGreaterThan(0);
            await Assert.That(report).IsNotNull();
            await Assert.That(report!.Characters).IsGreaterThan(0);
            await Assert.That(copy.GetCharacterCount(0)).IsEqualTo(0);
            await Assert.That(tab.TryGetDocument()!.GetCharacterCount(0)).IsEqualTo(before);
        }
    }

    /// <summary>Creates a window view model over a real source file.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="redactor">The redactor.</param>
    /// <param name="areas">The areas marked.</param>
    /// <returns>The view model.</returns>
    private static RedactionViewModel Create(TestServices test, FakeRedactor redactor, int areas) =>
        new(redactor, test.CreateDocument(SourceName, TestPdf.Create(1)), areas, areas > 0 ? MarkedPages : 0, false);
}
