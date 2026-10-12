// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Drives the redaction window: the warning is on screen, applying needs the confirmation, and the layout stays put through a run.</summary>
public sealed class RedactionWindowTests
{
    /// <summary>The areas marked.</summary>
    private const int Areas = 3;

    /// <summary>The pages marked.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 640;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 920;

    /// <summary>The warning and the confirmation are on screen before the run, apply is off until the box is ticked, and nothing moves during the run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarningConfirmationAndStableLayout()
    {
        using var test = new TestServices();
        var fake = new FakeRedactor { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new RedactionViewModel(fake, test.CreateDocument("report.pdf", TestPdf.Create(1)), Areas, Pages, true);
        var destination = Path.Combine(test.Directory, "report-redacted.pdf");
        var window = new RedactionWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible && window.FindControl<ListBox>("ImageList")!.ItemCount > 0);
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
            var warning = window.FindControl<TextBlock>("WarningText")!;
            var understand = window.FindControl<CheckBox>("UnderstandBox")!;
            var apply = window.FindControl<Button>("ApplyButton")!;
            var progress = window.FindControl<ProgressBar>("RunProgress")!;
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var images = window.FindControl<ListBox>("ImageList")!;
            var box = window.FindControl<Border>("ResultBox")!;
            var idleBounds = new[] { warning.Bounds, understand.Bounds, progress.Bounds, box.Bounds, images.Bounds };
            Capture(window, "redact-ready.png");

            await Assert.That(warning.Text).Contains("cannot be brought back");
            await Assert.That(window.FindControl<TextBlock>("SummaryText")!.Text).Contains("3 marked areas on 2 pages");
            await Assert.That(window.FindControl<TextBlock>("NoteText")!.Text).Contains("not saved yet");
            await Assert.That(apply.Command!.CanExecute(null)).IsFalse();

            understand.IsChecked = true;
            await Assert.That(apply.Command.CanExecute(null)).IsTrue();

            apply.Command.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.IsRunning && fake.Started.Task.IsCompleted);
            var runningBounds = new[] { warning.Bounds, understand.Bounds, progress.Bounds, box.Bounds, images.Bounds };
            Capture(window, "redact-running.png");

            await Assert.That(images.IsEnabled).IsFalse();
            await Assert.That(apply.Command.CanExecute(null)).IsFalse();

            fake.Gate.SetResult();
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Capture(window, "redact-done.png");

            await Assert.That(result.Text).Contains("Removed 120 text characters");
            await Assert.That(runningBounds).IsEquivalentTo(idleBounds);
            await Assert.That(new[] { warning.Bounds, understand.Bounds, progress.Bounds, box.Bounds, images.Bounds }).IsEquivalentTo(idleBounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control has a name, the confirmation can be reached with the keyboard, and status and errors are announced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsAreNamedAndAnnounced()
    {
        using var test = new TestServices();
        using var vm = new RedactionViewModel(new FakeRedactor(), test.CreateDocument("named.pdf", TestPdf.Create(1)), Areas, Pages, false);
        var window = new RedactionWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var understand = window.FindControl<CheckBox>("UnderstandBox")!;

            await Assert.That(understand.Focusable && understand.IsTabStop).IsTrue();
            await Assert.That(window.FindControl<TextBlock>("NoteText")!.IsVisible).IsFalse();
            await Assert.That(AutomationProperties.GetName(window.FindControl<TextBlock>("WarningText")!)).Contains("cannot be undone");
            await Assert.That(AutomationProperties.GetName(window.FindControl<ProgressBar>("RunProgress")!)).IsNotEmpty();
            await Assert.That(window.FindControl<Button>("ApplyButton")!.IsDefault).IsTrue();
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
