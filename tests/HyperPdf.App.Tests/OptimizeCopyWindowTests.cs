// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Drives the optimised copy window: its layout stays put, its controls are named and the run shows progress and a summary.</summary>
public sealed class OptimizeCopyWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The language suggested.</summary>
    private const string Language = "en-AU";

    /// <summary>The share of the progress bar a finished run reaches.</summary>
    private const double Complete = 100;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 640;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 900;

    /// <summary>The size choices, the options and the summary are on screen before, during and after a run, and none moves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutStaysStillThroughARun()
    {
        using var test = new TestServices();
        var fake = new FakeOptimizer { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new OptimizeCopyViewModel(fake, test.CreateDocument("report.pdf", TestPdf.Create(Pages)), Language);
        var destination = Path.Combine(test.Directory, "report-out.pdf");
        var window = new OptimizeCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible && window.FindControl<ListBox>("PresetList")!.ItemCount > 0);

            // The window's own picker handler is registered once it activates; the later handler is asked first.
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
            var list = window.FindControl<ListBox>("PresetList")!;
            var progress = window.FindControl<ProgressBar>("CopyProgress")!;
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var save = window.FindControl<Button>("SaveButton")!;
            var cancel = window.FindControl<Button>("CancelButton")!;
            var idleBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };
            Capture(window, "optimise-ready.png");

            await Assert.That(list.ItemCount).IsEqualTo(vm.Presets.Count);
            await Assert.That(ReferenceEquals(list.SelectedItem, vm.SelectedPreset)).IsTrue();
            await Assert.That(AutomationProperties.GetName(list.ContainerFromIndex(0)!)).Contains("Smaller file.");
            await Assert.That(window.FindControl<TextBlock>("NoteText")).IsNull();
            await Assert.That(save.Command!.CanExecute(null)).IsTrue();
            await Assert.That(cancel.Command!.CanExecute(null)).IsFalse();

            save.Command.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.IsRunning && fake.Started.Task.IsCompleted);
            var runningBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };
            Capture(window, "optimise-running.png");

            await Assert.That(list.IsEnabled).IsFalse();
            await Assert.That(window.FindControl<CheckBox>("FixBox")!.IsEnabled).IsFalse();
            await Assert.That(save.Command.CanExecute(null)).IsFalse();
            await Assert.That(cancel.Command.CanExecute(null)).IsTrue();

            fake.Gate.SetResult();
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Capture(window, "optimise-done.png");

            await Assert.That(progress.Value).IsEqualTo(Complete);
            await Assert.That(result.Text).Contains("Saved 50%");
            await Assert.That(window.FindControl<TextBlock>("StatusText")!.Text).IsEqualTo("Done. The copy is saved.");
            await Assert.That(list.IsEnabled).IsTrue();
            await Assert.That(runningBounds).IsEquivalentTo(idleBounds);
            await Assert.That(new[] { list.Bounds, progress.Bounds, result.Bounds }).IsEquivalentTo(idleBounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control can be reached with the keyboard, in reading order, and has a name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsAreNamedAndInOrder()
    {
        using var test = new TestServices();
        using var vm = new OptimizeCopyViewModel(new FakeOptimizer(), test.CreateDocument("named.pdf", TestPdf.Create(Pages)), Language);
        var window = new OptimizeCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var names = new[] { "FixBox", "TagsBox", "CleanBox" };

            foreach (var name in names)
            {
                var box = window.FindControl<CheckBox>(name)!;
                await Assert.That(box.Content as string).IsNotNull();
                await Assert.That(box.Focusable && box.IsTabStop).IsTrue();
            }

            await Assert.That(AutomationProperties.GetLabeledBy(window.FindControl<TextBox>("LanguageBox")!)).IsNotNull();
            await Assert.That(AutomationProperties.GetName(window.FindControl<ProgressBar>("CopyProgress")!)).IsNotEmpty();
            await Assert.That(window.FindControl<Button>("SaveButton")!.IsDefault).IsTrue();
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
