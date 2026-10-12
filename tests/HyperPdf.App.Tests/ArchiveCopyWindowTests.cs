// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Drives the PDF/A copy window: its layout stays put, its controls are named and a run shows a summary.</summary>
public sealed class ArchiveCopyWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 640;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 820;

    /// <summary>The name of the level list.</summary>
    private const string LevelListName = "LevelList";

    /// <summary>The name of the save button.</summary>
    private const string SaveButtonName = "SaveButton";

    /// <summary>The name of the progress bar.</summary>
    private const string ProgressName = "CopyProgress";

    /// <summary>The levels and the summary are on screen before, during and after a run, and none moves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutStaysStillThroughARun()
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new ArchiveCopyViewModel(fake, test.CreateDocument("minutes.pdf", TestPdf.Create(Pages)));
        var destination = Path.Combine(test.Directory, "minutes-archive.pdf");
        var window = new ArchiveCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible && window.FindControl<ListBox>(LevelListName)!.ItemCount > 0);

            // The window's own picker handler is registered once it activates; the later handler is asked first.
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
            var list = window.FindControl<ListBox>(LevelListName)!;
            var progress = window.FindControl<ProgressBar>(ProgressName)!;
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var save = window.FindControl<Button>(SaveButtonName)!;
            var cancel = window.FindControl<Button>("CancelButton")!;
            var idleBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };
            Capture(window, "pdfa-copy-ready.png");

            await Assert.That(list.ItemCount).IsEqualTo(vm.Levels.Count);
            await Assert.That(ReferenceEquals(list.SelectedItem, vm.SelectedLevel)).IsTrue();
            await Assert.That(AutomationProperties.GetName(list.ContainerFromIndex(1)!)).StartsWith("PDF/A-2b (suggested).");
            await Assert.That(progress.IsVisible).IsTrue();
            await Assert.That(save.Command!.CanExecute(null)).IsTrue();
            await Assert.That(cancel.Command!.CanExecute(null)).IsFalse();

            save.Command.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.IsRunning && fake.Started.Task.IsCompleted);
            var runningBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };
            Capture(window, "pdfa-copy-running.png");

            await Assert.That(list.IsEnabled).IsFalse();
            await Assert.That(progress.IsVisible).IsFalse();
            await Assert.That(window.FindControl<ProgressBar>("BusyProgress")!.IsVisible).IsTrue();
            await Assert.That(save.Command.CanExecute(null)).IsFalse();
            await Assert.That(cancel.Command.CanExecute(null)).IsTrue();

            fake.Gate.SetResult();
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Capture(window, "pdfa-copy-done.png");

            await Assert.That(result.Text).Contains("The copy meets PDF/A-2b.");
            await Assert.That(window.FindControl<TextBlock>("StatusText")!.Text).IsEqualTo("Done. The copy is saved.");
            await Assert.That(list.IsEnabled).IsTrue();
            await Assert.That(progress.IsVisible).IsTrue();
            await Assert.That(progress.Value).IsEqualTo(progress.Maximum);
            await Assert.That(runningBounds).IsEquivalentTo(idleBounds);
            await Assert.That(new[] { list.Bounds, progress.Bounds, result.Bounds }).IsEquivalentTo(idleBounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A refused copy shows its reasons in the summary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalShowsTheReasons()
    {
        using var test = new TestServices();
        var fake = new FakeArchiveCopyWriter { Problems = [new("The file is signed. Making a PDF/A copy would break the signatures.", "6.4.3")] };
        using var vm = new ArchiveCopyViewModel(fake, test.CreateDocument("signed.pdf", TestPdf.Create(Pages)));
        var window = new ArchiveCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "signed-archive.pdf")));
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var progress = window.FindControl<ProgressBar>(ProgressName)!;

            window.FindControl<Button>(SaveButtonName)!.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Capture(window, "pdfa-copy-refused.png");

            await Assert.That(progress.IsVisible).IsTrue();
            await Assert.That(progress.Value).IsEqualTo(progress.Minimum);
            await Assert.That(result.Text).Contains("so no file was saved");
            await Assert.That(result.Text).Contains("• The file is signed.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control has a name, the buttons have defaults and the status lines are announced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsAreNamed()
    {
        using var test = new TestServices();
        using var vm = new ArchiveCopyViewModel(new FakeArchiveCopyWriter(), test.CreateDocument("named.pdf", TestPdf.Create(Pages)));
        var window = new ArchiveCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);

            await Assert.That(AutomationProperties.GetName(window.FindControl<ListBox>(LevelListName)!)).IsNotEmpty();
            await Assert.That(AutomationProperties.GetName(window.FindControl<ProgressBar>(ProgressName)!)).IsNotEmpty();
            await Assert.That(window.FindControl<Button>(SaveButtonName)!.IsDefault).IsTrue();
            await Assert.That(window.FindControl<Button>("CloseButton")!.IsCancel).IsTrue();
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("StatusText")!)).IsEqualTo(AutomationLiveSetting.Polite);
            await Assert.That(AutomationProperties.GetLiveSetting(window.FindControl<TextBlock>("ErrorText")!)).IsEqualTo(AutomationLiveSetting.Assertive);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saves the window's frame when <c>HYPERPDF_SCREENSHOTS</c> names a folder.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The file name.</param>
    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("HYPERPDF_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        using var frame = window.CaptureRenderedFrame();
        _ = Directory.CreateDirectory(directory);
        frame?.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
