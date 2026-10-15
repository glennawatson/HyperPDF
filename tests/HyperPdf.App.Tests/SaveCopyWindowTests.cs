// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the unified Save a Copy window and the grouped Document menu with its Protect &amp; Sign submenu.</summary>
public sealed class SaveCopyWindowTests
{
    /// <summary>The pages of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The number of copy formats offered in the unified window.</summary>
    private const int FormatCount = 5;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 700;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 620;

    /// <summary>The main window width.</summary>
    private const double MainWindowWidth = 1280;

    /// <summary>The main window height.</summary>
    private const double MainWindowHeight = 800;

    /// <summary>Switching formats shows the matching options panel, updates the suggested file name, and keeps the bottom summary still.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SwitchesFormatPanelsAndSavesCopies()
    {
        using var test = new TestServices();
        var sourcePath = test.CreateDocument("report.pdf", TestPdf.Create(Pages));
        using var vm = CreateViewModel(sourcePath);
        var window = new SaveCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var progress = window.FindControl<ProgressBar>("CopyProgress")!;
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var idleBounds = new[] { progress.Bounds, result.Bounds };

            await Assert.That(vm.Formats.Count).IsEqualTo(FormatCount);
            await Assert.That(window.FindControl<StackPanel>("StandardPanel")!.IsVisible).IsTrue();
            await Assert.That(vm.SuggestedName).IsEqualTo("report (copy).pdf");

            vm.SelectFormat(SaveCopyFormat.Optimized);
            _ = await UiWait.UntilAsync(() => window.FindControl<StackPanel>("OptimizePanel")!.IsVisible);
            await Assert.That(vm.SuggestedName).IsEqualTo("report (optimised).pdf");

            vm.SelectFormat(SaveCopyFormat.Archive);
            _ = await UiWait.UntilAsync(() => window.FindControl<StackPanel>("ArchivePanel")!.IsVisible);
            await Assert.That(vm.SuggestedName).IsEqualTo("report (PDF-A).pdf");

            vm.SelectFormat(SaveCopyFormat.Pdf20);
            _ = await UiWait.UntilAsync(() => window.FindControl<StackPanel>("Pdf20Panel")!.IsVisible);
            await Assert.That(vm.SuggestedName).IsEqualTo("report (PDF 2.0).pdf");

            vm.SelectFormat(SaveCopyFormat.Flattened);
            _ = await UiWait.UntilAsync(() => window.FindControl<StackPanel>("FlattenPanel")!.IsVisible);
            await Assert.That(vm.SuggestedName).IsEqualTo("report (flattened).pdf");

            vm.SelectFormat(SaveCopyFormat.Standard);
            _ = await UiWait.UntilAsync(() => window.FindControl<StackPanel>("StandardPanel")!.IsVisible);
            var copyPath = Path.Combine(test.Directory, "report-standard-copy.pdf");
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(copyPath));
            _ = await vm.SaveCommand.Execute().ToTask();

            await Assert.That(File.Exists(copyPath)).IsTrue();
            await Assert.That(vm.SavedPath).IsEqualTo(copyPath);
            await Assert.That(result.Text).Contains("Saved a standard PDF copy");
            await Assert.That(new[] { progress.Bounds, result.Bounds }).IsEquivalentTo(idleBounds);
            await Assert.That(AutomationProperties.GetName(progress)).IsNotEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saving a standard copy refuses to overwrite the open file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardCopyRefusesOverwritingOpenFile()
    {
        using var test = new TestServices();
        var sourcePath = test.CreateDocument("open.pdf", TestPdf.Create(Pages));
        using var vm = CreateViewModel(sourcePath);
        using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(sourcePath));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(vm.Error).Contains("replace the file you have open");
        await Assert.That(vm.SavedPath).IsNull();
    }

    /// <summary>The Document menu groups Save a Copy and puts protection, redaction and signing under Protect &amp; Sign.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentMenuGroupsSaveCopyAndProtectAndSign()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("menu.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = MainWindowWidth, Height = MainWindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentMenuView>().Any());
            var menu = window.GetVisualDescendants().OfType<DocumentMenuView>().Single();

            await Assert.That(menu.SaveCopyItem.Command).IsNotNull();
            await Assert.That(menu.SaveCopyItem.Command!.CanExecute(null)).IsTrue();
            await Assert.That(menu.ProtectSignMenu.Header).IsEqualTo("_Protect & Sign");
            await Assert.That(menu.ProtectItem.Command).IsNotNull();
            await Assert.That(menu.RemoveProtectionItem.Command).IsNotNull();
            await Assert.That(menu.ApplyRedactionsItem.Command).IsNotNull();
            await Assert.That(menu.ValidationDataItem.Command).IsNotNull();
            await Assert.That(menu.ExternalSignItem.Command).IsNotNull();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Creates a <see cref="SaveCopyViewModel"/> backed by fake copy writers for testing.</summary>
    /// <param name="sourcePath">The source document path.</param>
    /// <returns>The view model.</returns>
    private static SaveCopyViewModel CreateViewModel(string sourcePath) =>
        new(
            new(new FakeOptimizer(), sourcePath, "en-AU"),
            new(new FakeArchiveCopyWriter(), sourcePath),
            new(new FakePdf20CopyWriter(), sourcePath),
            new(new FakeFlattenCopyWriter(), sourcePath),
            sourcePath,
            null,
            null);
}
