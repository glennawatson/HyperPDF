// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Optimizing;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="OptimizeCopyViewModel"/>, its report text and the tab command that opens it.</summary>
public sealed class OptimizeCopyViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "contract.pdf";

    /// <summary>The language suggested.</summary>
    private const string Language = "en-AU";

    /// <summary>The share of the progress bar a finished run reaches.</summary>
    private const double Complete = 100;

    /// <summary>The figures an inferred structure leaves without alternative text.</summary>
    private const int Figures = 3;

    /// <summary>The PDF/A part a document claims.</summary>
    private const int PdfAPart = 2;

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".optimising";

    /// <summary>The items in each step of the progress checks.</summary>
    private const int StepItems = 2;

    /// <summary>The longest wait for a run to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets the presets and the library-side choice each must reach, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The presets.</returns>
    public static IEnumerable<OptimizePreset> Presets() => [OptimizePreset.Smaller, OptimizePreset.Balanced, OptimizePreset.KeepQuality];

    /// <summary>Each size choice and every option reaches the optimiser.</summary>
    /// <param name="preset">The preset chosen.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Presets))]
    public async Task ChoicesReachTheOptimiser(OptimizePreset preset)
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer();
        using var vm = Create(test, fake);
        vm.SelectedPreset = vm.Presets.Single(p => p.Preset == preset);
        vm.FixAccessibility = false;
        vm.Language = "fr-FR";
        vm.AddInferredTags = true;
        vm.CleanUp = true;
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "out.pdf")));

        _ = await vm.SaveCommand.Execute().ToTask();

        OptimizeSettings expected = new(preset, false, "fr-FR", true, true);
        await Assert.That(fake.Received).IsEqualTo(expected);
    }

    /// <summary>The window starts balanced, with the accessibility entries on and the suggested language filled in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsBalancedWithPlainDescriptions()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());
        OptimizeSettings expected = new(OptimizePreset.Balanced, true, Language, false, false);

        using (Assert.Multiple())
        {
            await Assert.That(vm.Settings).IsEqualTo(expected);
            await Assert.That(vm.Presets.Count).IsEqualTo(Presets().Count());
            await Assert.That(vm.Presets.All(static p => p.Description.Length > p.Name.Length)).IsTrue();
            await Assert.That(vm.SuggestedName).IsEqualTo("contract (optimised).pdf");
        }
    }

    /// <summary>Saving writes the copy beside the chosen name, leaves no temporary file and explains the result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheCopyAndSummarises()
    {
        using var test = new TestServices();
        OptimizeReport report = new(FakeOptimizer.SourceBytes, FakeOptimizer.NewBytes, OptimizeWriteMode.Rewritten, ["Check the tags."], [new(OptimizeArea.Fonts, "Fonts are locked.")]);
        var fake = new FakeOptimizer { Report = report };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "out.pdf");
        string? suggested = null;
        using var handler = vm.SaveInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(destination);
        });

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(suggested).IsEqualTo("contract (optimised).pdf");
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
            await Assert.That(vm.IsRunning).IsFalse();
            await Assert.That(vm.ProgressPercent).IsEqualTo(Complete);
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
            await Assert.That(vm.ResultText).Contains("Saved 50%");
            await Assert.That(vm.ResultText).Contains("4.0 MB");
            await Assert.That(vm.ResultText).Contains("Left alone, fonts: Fonts are locked.");
            await Assert.That(vm.ResultText).Contains("Note: Check the tags.");
            await Assert.That(vm.Error).IsNull();
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer();
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

    /// <summary>Closing the file picker without a name does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickerCancelledDoesNothing()
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer();
        using var vm = Create(test, fake);
        using var handler = vm.SaveInteraction.RegisterHandler(static context => context.SetOutput(null));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(fake.Calls).IsEqualTo(0);
        await Assert.That(vm.IsRunning).IsFalse();
    }

    /// <summary>Cancel stops the run, removes the partial file and leaves the summary empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelStopsTheRunAndKeepsNoFile()
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
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
            await Assert.That(vm.ResultText).IsEmpty();
        }
    }

    /// <summary>A failed run says what went wrong in words and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureIsExplained()
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer { Failure = new InvalidDataException("The file is damaged.") };
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
            await Assert.That(vm.IsRunning).IsFalse();
        }
    }

    /// <summary>The report text says each outcome in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportTextIsPlain()
    {
        var saved = new OptimizeReport(FakeOptimizer.SourceBytes, FakeOptimizer.NewBytes, OptimizeWriteMode.Rewritten, [], []);
        var grew = saved with { BytesAfter = FakeOptimizer.SourceBytes + 1 };
        var same = saved with { BytesAfter = FakeOptimizer.SourceBytes };
        var copied = saved with { Mode = OptimizeWriteMode.Copied };
        var tagged = saved with { TagsInferred = true, FiguresNeedingAltText = Figures, WasSigned = true, IsEncrypted = true, PdfAPart = PdfAPart };

        var lines = OptimizeReportText.Join(OptimizeReportText.Lines(tagged, "/x/y.pdf"));

        using (Assert.Multiple())
        {
            await Assert.That(OptimizeReportText.Headline(saved)).IsEqualTo("Saved 50%. The file went from 4.0 MB to 2.0 MB.");
            await Assert.That(OptimizeReportText.Headline(grew)).Contains("a little larger");
            await Assert.That(OptimizeReportText.Headline(same)).Contains("stayed the same size");
            await Assert.That(OptimizeReportText.Headline(copied)).Contains("copy of the original");
            await Assert.That(lines).Contains("Saved as y.pdf.");
            await Assert.That(lines).Contains("3 pictures need alternative text");
            await Assert.That(lines).Contains("guessed from the page layout");
            await Assert.That(lines).Contains("signatures stay valid");
            await Assert.That(lines).Contains("encrypted in the same way");
            await Assert.That(lines).Contains("PDF/A part 2");
        }
    }

    /// <summary>The bar only moves forward across the steps.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressMovesForward()
    {
        var last = -1.0;
        foreach (var step in Enum.GetValues<OptimizeStep>())
        {
            var start = OptimizeReportText.OverallPercent(new(step, 0, StepItems));
            var middle = OptimizeReportText.OverallPercent(new(step, 1, StepItems));
            await Assert.That(start).IsGreaterThanOrEqualTo(last);
            await Assert.That(middle).IsGreaterThanOrEqualTo(start);
            last = middle;
        }

        await Assert.That(OptimizeReportText.OverallPercent(new(OptimizeStep.Done, 0, 0))).IsEqualTo(Complete);
    }

    /// <summary>The command is available with HyperPDF and explains its limit with the PDFium reference engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommandFollowsTheEngine()
    {
        using var pdfium = new TestServices(TestEngineChoice.Pdfium);
        using var pdfiumMain = new MainViewModel(pdfium.Services);
        pdfiumMain.Open([pdfium.CreateDocument(SourceName, Pages)]);
        using var native = new TestServices(TestEngineChoice.HyperPdf);
        using var nativeMain = new MainViewModel(native.Services);
        nativeMain.Open([native.CreateDocument(SourceName, Pages)]);

        var pdfiumTab = pdfiumMain.SelectedTab!;
        var nativeTab = nativeMain.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => pdfiumTab.IsLoaded && nativeTab.IsLoaded)).IsTrue();
        var pdfiumCan = ((ICommand)pdfiumTab.OptimizeCopyCommand).CanExecute(null);

        using (Assert.Multiple())
        {
            await Assert.That(pdfiumTab.CanOptimize).IsFalse();
            await Assert.That(pdfiumCan).IsFalse();
            await Assert.That(pdfiumTab.OptimizeMenuText).Contains("needs the HyperPDF engine");
            await Assert.That(nativeTab.CanOptimize).IsTrue();
            await Assert.That(nativeTab.OptimizeMenuText).IsEqualTo("Save _Optimised Copy…");
        }
    }

    /// <summary>On HyperPDF the command opens the window and a real optimised copy reopens with the same pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealCopyReopensWithTheSamePages()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var destination = Path.Combine(test.Directory, "real.pdf");
        OptimizeReport? report = null;
        using var handler = tab.OptimizeInteraction.RegisterHandler(async context =>
        {
            using var picker = context.Input.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            _ = await context.Input.SaveCommand.Execute().ToTask();
            report = context.Input.Report;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.OptimizeCopyCommand.Execute().ToTask();

        await TestServices.OpenAndWaitAsync(main, [destination]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        await Assert.That(report).IsNotNull();
        await Assert.That(File.Exists(destination)).IsTrue();
        await Assert.That(main.Tabs.Single(t => t.FilePath == destination).Source.PageSizes.Length).IsEqualTo(Pages);
    }

    /// <summary>Gets the temporary file a copy is written to before it is moved into place.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The temporary path.</returns>
    private static string Temporary(string destination) => $"{destination}{TemporarySuffix}";

    /// <summary>Creates a window view model over a real source file.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="optimizer">The optimiser.</param>
    /// <returns>The view model.</returns>
    private static OptimizeCopyViewModel Create(TestServices test, FakeOptimizer optimizer) =>
        new(optimizer, test.CreateDocument(SourceName, TestPdf.Create(Pages)), Language);
}
