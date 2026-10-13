// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;

namespace HyperPdf.App.Tests;

/// <summary>Drives the validation data and timestamp window: its layout stays put, its controls are named and a run shows every note.</summary>
public sealed class ValidationDataWindowTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 640;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 760;

    /// <summary>The name of the list of choices.</summary>
    private const string ActionListName = "ActionList";

    /// <summary>The choices and the summary are on screen before, during and after a run, and none moves; the notes fill the summary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutStaysStillAndNotesAreShown()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously), Notes = ["The security store now holds 2 certificates."] };
        using var vm = new ValidationDataViewModel(fake, test.CreateDocument("minutes.pdf", Pages), new NoRevocationSource(), null);
        var window = new ValidationDataWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible && window.FindControl<ListBox>(ActionListName)!.ItemCount > 0);
            using var picker = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "minutes-validation.pdf")));
            var list = window.FindControl<ListBox>(ActionListName)!;
            var progress = window.FindControl<ProgressBar>("CopyProgress")!;
            var result = window.FindControl<SelectableTextBlock>("ResultText")!;
            var save = window.FindControl<Button>("SaveButton")!;
            var records = window.FindControl<CheckBox>("VriBox")!;
            var idleBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };

            await Assert.That(list.ItemCount).IsEqualTo(vm.Actions.Count);
            await Assert.That(ReferenceEquals(list.SelectedItem, vm.SelectedAction)).IsTrue();
            await Assert.That(AutomationProperties.GetName(list.ContainerFromIndex(0)!)).StartsWith("Add validation data.");
            await Assert.That(records.IsEnabled).IsTrue();

            save.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => vm.IsRunning && fake.Started.Task.IsCompleted);
            var runningBounds = new[] { list.Bounds, progress.Bounds, result.Bounds };

            await Assert.That(list.IsEnabled).IsFalse();
            await Assert.That(records.IsEnabled).IsFalse();
            await Assert.That(window.FindControl<ProgressBar>("BusyProgress")!.IsVisible).IsTrue();

            fake.Gate.SetResult();
            _ = await UiWait.UntilAsync(() => !vm.IsRunning && result.Text?.Length > 0);

            await Assert.That(result.Text).Contains("Saved as minutes-validation.pdf.");
            await Assert.That(result.Text).Contains("• The security store now holds 2 certificates.");
            await Assert.That(list.IsEnabled).IsTrue();
            await Assert.That(runningBounds).IsEquivalentTo(idleBounds);
            await Assert.That(new[] { list.Bounds, progress.Bounds, result.Bounds }).IsEquivalentTo(idleBounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The record box is only on for validation data, and choosing a timestamp turns it off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordBoxFollowsTheChoice()
    {
        using var test = new TestServices();
        using var vm = new ValidationDataViewModel(new FakeDocumentSigner(), test.CreateDocument("choice.pdf", Pages), new NoRevocationSource(), null);
        var window = new ValidationDataWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);
            var records = window.FindControl<CheckBox>("VriBox")!;
            vm.SelectedAction = vm.Actions[1];
            var off = await UiWait.UntilAsync(() => !records.IsEnabled);
            vm.SelectedAction = vm.Actions[0];
            var on = await UiWait.UntilAsync(() => records.IsEnabled);

            await Assert.That(off).IsTrue();
            await Assert.That(on).IsTrue();
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
        using var vm = new ValidationDataViewModel(new FakeDocumentSigner(), test.CreateDocument("named.pdf", Pages), new NoRevocationSource(), null);
        var window = new ValidationDataWindow { ViewModel = vm, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);

            var actions = window.FindControl<ListBox>(ActionListName)!;
            var focused = await UiWait.UntilAsync(() => actions.IsKeyboardFocusWithin);

            await Assert.That(focused).IsTrue();
            await Assert.That(AutomationProperties.GetName(window.FindControl<ListBox>(ActionListName)!)).IsNotEmpty();
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
}
