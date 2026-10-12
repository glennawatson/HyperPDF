// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Redaction;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Tests for <see cref="ArchiveCopyViewModel"/>, its text, the tab command that opens it and the removed-claim warnings.</summary>
public sealed class ArchiveCopyViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "letter.pdf";

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".archiving";

    /// <summary>The PDF/A part a document claims.</summary>
    private const int ClaimedPart = 2;

    /// <summary>The longest wait for a run to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets the levels the window offers, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The levels.</returns>
    public static IEnumerable<ArchiveLevel> OfferedLevels() =>
    [
        ArchiveLevel.PdfA1B, ArchiveLevel.PdfA2B, ArchiveLevel.PdfA2U, ArchiveLevel.PdfA2A, ArchiveLevel.PdfA3B,
        ArchiveLevel.PdfA3U, ArchiveLevel.PdfA3A, ArchiveLevel.PdfA4, ArchiveLevel.PdfA4E, ArchiveLevel.PdfA4F,
    ];

    /// <summary>The window offers the asked-for levels with PDF/A-2b suggested and plain descriptions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffersTheLevelsWithPdfA2BSuggested()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());

        using (Assert.Multiple())
        {
            await Assert.That(vm.Levels.Select(static l => l.Level)).IsEquivalentTo(OfferedLevels());
            await Assert.That(vm.Level).IsEqualTo(ArchiveLevel.PdfA2B);
            await Assert.That(vm.Levels.All(static l => l.Description.Length > l.Name.Length)).IsTrue();
            await Assert.That(vm.SuggestedName).IsEqualTo("letter (PDF-A).pdf");
        }
    }

    /// <summary>The chosen level reaches the writer.</summary>
    /// <param name="level">The level chosen.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(OfferedLevels))]
    public async Task ChosenLevelReachesTheWriter(ArchiveLevel level)
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter();
        using var vm = Create(test, fake);
        vm.SelectedLevel = vm.Levels.Single(l => l.Level == level);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "out.pdf")));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(fake.Received).IsEqualTo(level);
    }

    /// <summary>A made copy is moved into place, leaves no temporary file and lists what changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheCopyAndListsTheChanges()
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter();
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
            await Assert.That(vm.ResultText).Contains("The copy meets PDF/A-2b.");
            await Assert.That(vm.ResultText).Contains("• Added a colour profile.");
            await Assert.That(vm.Error).IsNull();
        }
    }

    /// <summary>A copy that cannot be made saves no file and lists the reasons in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalSavesNothingAndExplains()
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter { Problems = [new("The font Symbol is not embedded and no matching font is available to embed.", "6.2.11.4.1")] };
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
            await Assert.That(vm.ResultText).Contains("could not be made as PDF/A-2b, so no file was saved");
            await Assert.That(vm.ResultText).Contains("• The font Symbol is not embedded");
            await Assert.That(vm.ResultText).DoesNotContain("6.2.11.4.1");
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter();
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
        var fake = new FakeArchiveCopyWriter { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
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
        var fake = new FakeArchiveCopyWriter { Failure = new InvalidDataException("The file is damaged.") };
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

    /// <summary>The optimised and redacted copy summaries warn when the copy lost its PDF/A claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiesWarnWhenTheClaimWasRemoved()
    {
        var removal = new ArchiveClaimRemoval(ArchiveLevel.PdfA2B, ["Fonts shall be embedded."]);
        var optimized = new OptimizeReport(FakeOptimizer.SourceBytes, FakeOptimizer.NewBytes, OptimizeWriteMode.Rewritten, [], []) { PdfAPart = ClaimedPart, ClaimRemoved = removal };

        var lines = OptimizeReportText.Join(OptimizeReportText.Lines(optimized, "/x/y.pdf"));

        using (Assert.Multiple())
        {
            await Assert.That(lines).Contains("This file no longer met PDF/A-2b, so HyperPDF removed its PDF/A claim. Use Save PDF/A Copy to make a PDF/A version.");
            await Assert.That(lines).DoesNotContain("only changes that keep it valid");
            await Assert.That(ArchiveText.Label(ArchiveLevel.PdfA4F)).IsEqualTo("PDF/A-4f");
            await Assert.That(new RedactionReport(1, 1, 1, 0, 0, 0) { ClaimRemoved = removal }.ClaimRemoved).IsEqualTo(removal);
        }
    }

    /// <summary>The copy summaries say when the copy was fixed to keep its PDF/A level, naming a few fixes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiesNoteWhenTheClaimWasRepaired()
    {
        var repair = new ArchiveClaimRepair(ArchiveLevel.PdfA2B, ["Added a colour profile.", "Drew form appearances.", "Removed a script.", "Embedded a font."]);
        var optimized = new OptimizeReport(FakeOptimizer.SourceBytes, FakeOptimizer.NewBytes, OptimizeWriteMode.Rewritten, [], []) { PdfAPart = ClaimedPart, ClaimRepaired = repair };

        var lines = OptimizeReportText.Join(OptimizeReportText.Lines(optimized, "/x/y.pdf"));

        using (Assert.Multiple())
        {
            await Assert.That(lines).Contains(
                "HyperPDF fixed the saved file so it still meets PDF/A-2b. Added a colour profile. Drew form appearances. Removed a script. And 1 more fixes.");
            await Assert.That(lines).DoesNotContain("only changes that keep it valid");
            await Assert.That(new RedactionReport(1, 1, 1, 0, 0, 0) { ClaimRepaired = repair }.ClaimRepaired).IsEqualTo(repair);
            await Assert.That(ArchiveText.ClaimRepairedNotice(repair with { Repairs = [] })).IsEqualTo("HyperPDF fixed the saved file so it still meets PDF/A-2b.");
        }
    }

    /// <summary>The removal warning names the first reason the file could not be fixed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovalWarningGivesTheReason()
    {
        var removal = new ArchiveClaimRemoval(ArchiveLevel.PdfA2B, ["Fonts shall be embedded."]) { Reasons = ["The file is signed."] };

        await Assert.That(ArchiveText.ClaimRemovedNotice(removal)).IsEqualTo(
            "This file no longer met PDF/A-2b, so HyperPDF removed its PDF/A claim. It could not be fixed: The file is signed. Use Save PDF/A Copy to make a PDF/A version.");
    }

    /// <summary>Saving a file that no longer meets its PDF/A claim and cannot be fixed tells the reader the claim was removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveWarnsWhenItRemovesTheClaim()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("claimed.pdf", TestPdf.CreateWithUnrepairablePdfAClaim())]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        var saved = await tab.SaveAsync(Path.Combine(test.Directory, "claimed-saved.pdf"), CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(tab.Notice).StartsWith("This file no longer met PDF/A-2b, so HyperPDF removed its PDF/A claim. It could not be fixed: ");
        await Assert.That(tab.Notice).EndsWith(" Use Save PDF/A Copy to make a PDF/A version.");
    }

    /// <summary>Saving a file that no longer meets its PDF/A claim but can be fixed tells the reader it was fixed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveSaysWhenItRepairsTheFile()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("claimed.pdf", TestPdf.CreateWithFalsePdfAClaim())]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        var saved = await tab.SaveAsync(Path.Combine(test.Directory, "claimed-saved.pdf"), CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(tab.Notice).StartsWith("HyperPDF fixed the saved file so it still meets PDF/A-2b.");
    }

    /// <summary>On HyperPDF the command is on, opens the window and a real PDF/A copy reopens with the same pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealCopyReopensWithTheSamePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.Archive.CanRun).IsTrue();
        await Assert.That(tab.Archive.MenuText).IsEqualTo("Save P_DF/A Copy…");
        var destination = Path.Combine(test.Directory, "archived.pdf");
        ArchiveCopyResult? result = null;
        using var handler = tab.Archive.Interaction.RegisterHandler(async context =>
        {
            using var picker = context.Input.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            _ = await context.Input.SaveCommand.Execute().ToTask();
            result = context.Input.Result;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Archive.RunCommand.Execute().ToTask();

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
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
    private static ArchiveCopyViewModel Create(TestServices test, FakeArchiveCopyWriter writer) =>
        new(writer, test.CreateDocument(SourceName, TestPdf.Create(Pages)));
}
