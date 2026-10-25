// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Exporting;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the Export Pages window's state: choices, numbered files, refusing and failing, and a real export through the engine.</summary>
public sealed class ExportPagesViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The zero based last page of the generated documents.</summary>
    private const int LastPage = 2;

    /// <summary>The things the fake export says the format left out.</summary>
    private const int SkippedThings = 2;

    /// <summary>The zero based twelfth page.</summary>
    private const int TwelfthPage = 11;

    /// <summary>The zero based fifth page.</summary>
    private const int FifthPage = 4;

    /// <summary>The letters of "PNG" after the first byte of a PNG file.</summary>
    private const int PngNameLength = 3;

    /// <summary>The document name.</summary>
    private const string SourceName = "report.pdf";

    /// <summary>The ending of the temporary file an export is written to.</summary>
    private const string TemporarySuffix = ".export";

    /// <summary>The resolution used by the real export, kept low so the pictures are small.</summary>
    private const int LowDpi = 36;

    /// <summary>The window starts on the page on screen, with a name after the open file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsOnThePageOnScreen()
    {
        using var test = new TestServices();
        using var vm = Create(test, new(), 1);
        var pages = new List<int>();

        using (Assert.Multiple())
        {
            await Assert.That(vm.TryGetPages(pages)).IsTrue();
            await Assert.That(pages).IsEquivalentTo([1]);
            await Assert.That(vm.PagesText).IsEqualTo("2");
            await Assert.That(vm.SuggestedName).IsEqualTo("report.png");
            await Assert.That(vm.StatusText).StartsWith("Ready.");
        }
    }

    /// <summary>The page choices name the pages they say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesPagesByScope()
    {
        using var test = new TestServices();
        using var vm = Create(test, new(), 0);
        var pages = new List<int>();

        vm.SelectedScope = vm.Scopes[1];
        await Assert.That(vm.TryGetPages(pages)).IsTrue();
        await Assert.That(pages).IsEquivalentTo([0, 1, LastPage]);

        vm.SelectedScope = vm.Scopes[2];
        vm.PagesText = "1, 3";
        await Assert.That(vm.TryGetPages(pages)).IsTrue();
        await Assert.That(pages).IsEquivalentTo([0, LastPage]);

        vm.PagesText = "9";
        await Assert.That(vm.TryGetPages(pages)).IsFalse();
    }

    /// <summary>A format that holds one page writes a numbered file for each page chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NumbersTheFilesOfAOnePageFormat()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter();
        using var vm = Create(test, fake, 0);
        vm.SelectedScope = vm.Scopes[1];
        var destination = Path.Combine(test.Directory, "out.png");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls.Count).IsEqualTo(Pages);
            await Assert.That(File.Exists(Path.Combine(test.Directory, "out-001.png"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(test.Directory, "out-003.png"))).IsTrue();
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(vm.StatusText).IsEqualTo("Done. The export is saved.");
            await Assert.That(vm.ResultText).Contains("3 files");
        }
    }

    /// <summary>PostScript holds every page, so it writes one file with all the pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesOneFileForPostScript()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter { Result = new(true, SkippedThings, 1, null) };
        using var vm = Create(test, fake, 0);
        vm.SelectedFormat = PageFileFormats.Get(PageFileFormat.PostScript);
        vm.SelectedScope = vm.Scopes[1];
        var destination = Path.Combine(test.Directory, "all.ps");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls.Count).IsEqualTo(1);
            await Assert.That(fake.Calls[0]).IsEquivalentTo([0, 1, LastPage]);
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
            await Assert.That(vm.ResultText).Contains("2 thing(s) this format cannot hold");
            await Assert.That(vm.ResultText).Contains("1 thing(s) were drawn a little differently");
        }
    }

    /// <summary>The chosen resolution and marks reach the exporter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PassesTheChosenOptions()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter();
        using var vm = Create(test, fake, 0);
        vm.Dpi = LowDpi;
        vm.IncludeMarkup = false;
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "one.png")));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(fake.LastOptions).IsEqualTo(new(LowDpi, false));
    }

    /// <summary>Pages that are not in the document and a resolution out of range are refused before anything is asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBadPagesAndResolution()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter();
        using var vm = Create(test, fake, 0);
        var asked = false;
        using var handler = vm.SaveInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(null);
        });
        vm.SelectedScope = vm.Scopes[2];
        vm.PagesText = "7";

        _ = await vm.SaveCommand.Execute().ToTask();
        await Assert.That(vm.Error).Contains("Type pages between 1 and 3");

        vm.PagesText = "1";
        vm.Dpi = 1;
        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).Contains("resolution between");
            await Assert.That(asked).IsFalse();
            await Assert.That(fake.Calls.Count).IsEqualTo(0);
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter();
        using var vm = Create(test, fake, 0);
        var source = Path.Combine(test.Directory, SourceName);
        var before = await File.ReadAllBytesAsync(source);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(source));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls.Count).IsEqualTo(0);
            await Assert.That(vm.Error).Contains("replace the file you have open");
            await Assert.That(await File.ReadAllBytesAsync(source)).IsEquivalentTo(before);
        }
    }

    /// <summary>A refusal from the exporter keeps no file and shows why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsWhyAnExportWasRefused()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter { Result = PageFileExportResult.Failed("That picture would be too large.") };
        using var vm = Create(test, fake, 0);
        var destination = Path.Combine(test.Directory, "big.png");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("That picture would be too large.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
        }
    }

    /// <summary>A failure while writing is shown in the window and leaves no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsAFailureAndKeepsNoFile()
    {
        using var test = new TestServices();
        var fake = new FakePageFileExporter { Failure = new IOException("The disk is full.") };
        using var vm = Create(test, fake, 0);
        var destination = Path.Combine(test.Directory, "full.png");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("Could not export: The disk is full.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
        }
    }

    /// <summary>Numbered paths keep the folder and ending and sort in page order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NumbersPathsInPageOrder()
    {
        await Assert.That(ExportPagesViewModel.NumberedPath("/data/out.png", 0)).IsEqualTo("/data/out-001.png");
        await Assert.That(ExportPagesViewModel.NumberedPath("/data/out.v2.tif", TwelfthPage)).IsEqualTo("/data/out.v2-012.tif");
        await Assert.That(ExportPagesViewModel.NumberedPath("/data/out", FifthPage)).IsEqualTo("/data/out-005");
    }

    /// <summary>On HyperPDF the command is on, and real PNG, SVG and PostScript files come out and read back as the right kind of file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealExportsWriteTheRightKindsOfFile()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.Copies.ExportPages.CanRun).IsTrue();
        var png = Path.Combine(test.Directory, "page.png");
        var svg = Path.Combine(test.Directory, "page.svg");
        var eps = Path.Combine(test.Directory, "page.eps");
        var ps = Path.Combine(test.Directory, "all.ps");
        using var handler = tab.Copies.ExportPages.Interaction.RegisterHandler(async context =>
        {
            var vm = context.Input;
            vm.Dpi = LowDpi;
            await ExportAsync(vm, PageFileFormat.Png, vm.Scopes[0], png);
            await ExportAsync(vm, PageFileFormat.Svg, vm.Scopes[0], svg);
            await ExportAsync(vm, PageFileFormat.EncapsulatedPostScript, vm.Scopes[0], eps);
            await ExportAsync(vm, PageFileFormat.PostScript, vm.Scopes[1], ps);
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Copies.ExportPages.RunCommand.Execute().ToTask();

        var pngBytes = await File.ReadAllBytesAsync(png);
        using (Assert.Multiple())
        {
            await Assert.That(pngBytes.AsSpan(1, PngNameLength).SequenceEqual("PNG"u8)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(svg)).Contains("<svg");
            await Assert.That(await File.ReadAllTextAsync(eps)).StartsWith("%!PS-Adobe");
            await Assert.That(await File.ReadAllTextAsync(eps)).Contains("EPSF");
            await Assert.That(await File.ReadAllTextAsync(ps)).StartsWith("%!PS-Adobe");
            await Assert.That(await File.ReadAllTextAsync(ps)).Contains("%%Pages: 3");
        }
    }

    /// <summary>Exports with one format and page choice to a path.</summary>
    /// <param name="vm">The window.</param>
    /// <param name="format">The format.</param>
    /// <param name="scope">The page choice.</param>
    /// <param name="path">The path to save to.</param>
    /// <returns>A task.</returns>
    private static async Task ExportAsync(ExportPagesViewModel vm, PageFileFormat format, ExportPagesScopeChoice scope, string path)
    {
        vm.SelectedFormat = PageFileFormats.Get(format);
        vm.SelectedScope = scope;
        using var picker = vm.SaveInteraction.RegisterHandler(pick => pick.SetOutput(path));
        _ = await vm.SaveCommand.Execute().ToTask();
        await Assert.That(vm.Error).IsNull().Because($"Exporting {format} failed.");
    }

    /// <summary>Creates a window view model over a real source file.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="exporter">The exporter.</param>
    /// <param name="currentPage">The zero based page on screen.</param>
    /// <returns>The view model.</returns>
    private static ExportPagesViewModel Create(TestServices test, FakePageFileExporter exporter, int currentPage) =>
        new(exporter, test.CreateDocument(SourceName, TestPdf.Create(Pages)), Pages, currentPage);
}
