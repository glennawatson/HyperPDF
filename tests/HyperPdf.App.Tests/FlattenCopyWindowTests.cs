// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using HyperPdf.App.Views;
using HyperPdf.Core.Flattening;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks the Save Flattened Copy window: a still layout, the options, the reasons of a refusal, and names for screen readers.</summary>
public sealed class FlattenCopyWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 680;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 860;

    /// <summary>The name of the save button.</summary>
    private const string SaveButtonName = "SaveButton";

    /// <summary>The name of the list of scopes.</summary>
    private const string ScopeListName = "ScopeList";

    /// <summary>The name of the list of looks.</summary>
    private const string LookListName = "LookList";

    /// <summary>The index of the suggested scope, form fields and all markup.</summary>
    private const int SuggestedScopeIndex = 2;

    /// <summary>The name of the progress bar.</summary>
    private const string ProgressName = "CopyProgress";

    /// <summary>The name of the summary.</summary>
    private const string ResultName = "ResultText";

    /// <summary>The summary is on screen before, during and after a run, nothing moves, and the options reach the writer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutStaysStillThroughARun()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new FlattenCopyViewModel(fake, test.CreateDocument("minutes.pdf", TestPdf.Create(Pages)));
        var destination = Path.Combine(test.Directory, "minutes-flat.pdf");
        var window = new FlattenCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
            var progress = window.FindControl<ProgressBar>(ProgressName)!;
            var result = window.FindControl<SelectableTextBlock>(ResultName)!;
            var save = window.FindControl<Button>(SaveButtonName)!;
            var cancel = window.FindControl<Button>("CancelButton")!;
            var scopes = window.FindControl<ListBox>(ScopeListName)!;
            var idleBounds = new[] { progress.Bounds, result.Bounds };
            Capture(window, "flatten-copy-ready.png");

            await Assert.That(scopes.SelectedItem).IsEqualTo(vm.Scopes[2]);
            await Assert.That(window.FindControl<ListBox>(LookListName)!.SelectedItem).IsEqualTo(vm.Looks[0]);
            await Assert.That(save.Command!.CanExecute(null)).IsTrue();
            await Assert.That(cancel.Command!.CanExecute(null)).IsFalse();

            scopes.SelectedItem = vm.Scopes[0];
            window.FindControl<CheckBox>("UprightBox")!.IsChecked = true;
            save.Command.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.IsRunning && fake.Started.Task.IsCompleted);
            var runningBounds = new[] { progress.Bounds, result.Bounds };
            Capture(window, "flatten-copy-running.png");

            await Assert.That(progress.IsVisible).IsFalse();
            await Assert.That(window.FindControl<ProgressBar>("BusyProgress")!.IsVisible).IsTrue();
            await Assert.That(scopes.IsEnabled).IsFalse();
            await Assert.That(cancel.Command.CanExecute(null)).IsTrue();

            fake.Gate.SetResult();
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Capture(window, "flatten-copy-done.png");

            await Assert.That(fake.LastOptions).IsEqualTo(new(FlattenCopyScope.FormFields, FlattenCopyLook.Screen, true));
            await Assert.That(result.Text).Contains("now part of the page");
            await Assert.That(progress.IsVisible).IsTrue();
            await Assert.That(progress.Value).IsEqualTo(progress.Maximum);
            await Assert.That(runningBounds).IsEquivalentTo(idleBounds);
            await Assert.That(new[] { progress.Bounds, result.Bounds }).IsEquivalentTo(idleBounds);
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
        var fake = new FakeFlattenCopyWriter { Problems = [new("The document is signed, so no copy was made.", "12.8")] };
        using var vm = new FlattenCopyViewModel(fake, test.CreateDocument("signed.pdf", TestPdf.Create(Pages)));
        var window = new FlattenCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "signed-flat.pdf")));
            var result = window.FindControl<SelectableTextBlock>(ResultName)!;
            var progress = window.FindControl<ProgressBar>(ProgressName)!;

            window.FindControl<Button>(SaveButtonName)!.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);
            Capture(window, "flatten-copy-refused.png");

            await Assert.That(progress.Value).IsEqualTo(progress.Minimum);
            await Assert.That(result.Text).Contains("so no file was saved");
            await Assert.That(result.Text).Contains("• The document is signed");
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
        using var vm = new FlattenCopyViewModel(new FakeFlattenCopyWriter(), test.CreateDocument("named.pdf", TestPdf.Create(Pages)));
        var window = new FlattenCopyWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var scopes = window.FindControl<ListBox>(ScopeListName)!;
            var looks = window.FindControl<ListBox>(LookListName)!;
            _ = await UiWait.UntilAsync(() => window.IsVisible && scopes.ContainerFromIndex(SuggestedScopeIndex) is not null && looks.ContainerFromIndex(0) is not null);

            await Assert.That(AutomationProperties.GetName(scopes)).IsNotEmpty();
            await Assert.That(AutomationProperties.GetName(looks)).IsNotEmpty();
            await Assert.That(AutomationProperties.GetName(scopes.ContainerFromIndex(SuggestedScopeIndex)!)).StartsWith("Form fields and all markup (suggested).");
            await Assert.That(AutomationProperties.GetName(looks.ContainerFromIndex(0)!)).StartsWith("As it looks on screen.");
            await Assert.That(AutomationProperties.GetName(window.FindControl<ProgressBar>(ProgressName)!)).IsNotEmpty();
            await Assert.That(AutomationProperties.GetName(window.FindControl<SelectableTextBlock>(ResultName)!)).IsNotEmpty();
            await Assert.That(window.FindControl<CheckBox>("UprightBox")!.Content).IsNotNull();
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
