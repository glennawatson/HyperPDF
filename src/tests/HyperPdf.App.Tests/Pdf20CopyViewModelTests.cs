// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the Save as PDF 2.0 Copy window's state: the suggested name, saving, refusing, cancelling and failing.</summary>
public sealed class Pdf20CopyViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "letter.pdf";

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".pdf20";

    /// <summary>The longest wait for a run to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The window names the copy after the open file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SuggestsAName()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());

        await Assert.That(vm.SuggestedName).IsEqualTo("letter (PDF 2.0).pdf");
        await Assert.That(vm.StatusText).StartsWith("Ready.");
    }

    /// <summary>A made copy is moved into place, leaves no temporary file and lists what changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheCopyAndListsTheChanges()
    {
        using var test = new TestServices();
        var fake = new FakePdf20CopyWriter();
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "kept.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
            await Assert.That(vm.StatusText).IsEqualTo("Done. The copy is saved.");
            await Assert.That(vm.ResultText).Contains("The copy is a PDF 2.0 file.");
            await Assert.That(vm.ResultText).Contains("• Moved the document information title to the XMP metadata.");
            await Assert.That(vm.Error).IsNull();
        }
    }

    /// <summary>A copy that cannot be made saves no file and lists the reasons in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalSavesNothingAndExplains()
    {
        using var test = new TestServices();
        var fake = new FakePdf20CopyWriter { Problems = [new("The document is signed, and a PDF 2.0 copy would break the signatures.", "12.8.1")] };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "refused.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
            await Assert.That(vm.SavedPath).IsNull();
            await Assert.That(vm.StatusText).Contains("could not be made");
            await Assert.That(vm.ResultText).Contains("could not be made as a PDF 2.0 file, so no file was saved");
            await Assert.That(vm.ResultText).Contains("• The document is signed");
            await Assert.That(vm.ResultText).DoesNotContain("12.8.1");
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakePdf20CopyWriter();
        using var vm = Create(test, fake);
        var source = Path.Combine(test.Directory, SourceName);
        var before = await File.ReadAllBytesAsync(source);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(source));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).IsEqualTo(0);
            await Assert.That(vm.Error).Contains("replace the file you have open");
            await Assert.That(await File.ReadAllBytesAsync(source)).IsEquivalentTo(before);
        }
    }

    /// <summary>Cancel stops the run and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelStopsTheRunAndKeepsNoFile()
    {
        using var test = new TestServices();
        var fake = new FakePdf20CopyWriter { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "cancelled.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        var run = vm.SaveCommand.Execute().ToTask();
        await fake.Started.Task.WaitAsync(StartTimeout);
        await Assert.That(vm.IsRunning).IsTrue();
        _ = await vm.CancelCommand.Execute().ToTask();
        _ = await run;

        using (Assert.Multiple())
        {
            await Assert.That(vm.IsRunning).IsFalse();
            await Assert.That(vm.StatusText).IsEqualTo("Cancelled. No file was saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
        }
    }

    /// <summary>A failed run says what went wrong in words and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureIsExplained()
    {
        using var test = new TestServices();
        var fake = new FakePdf20CopyWriter { Failure = new InvalidDataException("The file is damaged.") };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "failed.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("Could not save the copy: The file is damaged.");
            await Assert.That(vm.StatusText).IsEqualTo("The copy was not saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
        }
    }

    /// <summary>On HyperPDF the command is on, opens the window, and a real PDF 2.0 copy reopens with the same pages and a 2.0 header.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealCopyReopensWithTheSamePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.Copies.Pdf20.CanRun).IsTrue();
        await Assert.That(tab.Copies.Pdf20.MenuText).IsEqualTo("Save as PDF 2.0 _Copy…");
        var destination = Path.Combine(test.Directory, "pdf20.pdf");
        Pdf20CopyResult? result = null;
        using var handler = tab.Copies.Pdf20.Interaction.RegisterHandler(async context =>
        {
            using var picker = context.Input.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            _ = await context.Input.SaveCommand.Execute().ToTask();
            result = context.Input.Result;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Copies.Pdf20.RunCommand.Execute().ToTask();

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
        var bytes = await File.ReadAllBytesAsync(destination);
        await Assert.That(bytes.AsSpan().StartsWith("%PDF-2.0"u8)).IsTrue();
        await TestServices.OpenAndWaitAsync(main, [destination]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        await Assert.That(main.Tabs.Single(t => t.FilePath == destination).Source.PageSizes.Length).IsEqualTo(Pages);
    }

    /// <summary>Gets the temporary file a copy is written to before it is moved into place.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The temporary path.</returns>
    private static string Temporary(string destination) => $"{destination}{TemporarySuffix}";

    /// <summary>Creates a window view model over a real source file.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="writer">The writer.</param>
    /// <returns>The view model.</returns>
    private static Pdf20CopyViewModel Create(TestServices test, FakePdf20CopyWriter writer) =>
        new(writer, test.CreateDocument(SourceName, TestPdf.Create(Pages)));
}
