// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses a PDF/A level and writes a PDF/A copy of the open document. Every section is always on screen, so the layout
/// does not move while the copy is made; the levels are disabled during the run and the summary fills in when it ends.
/// </summary>
[DebuggerDisplay("ArchiveCopyWindow: {Title}")]
public sealed partial class ArchiveCopyWindow : ReactiveUI.Avalonia.ReactiveWindow<ArchiveCopyViewModel>
{
    /// <summary>The vertical space between a choice's name and its description.</summary>
    private const double ChoiceGap = 2;

    /// <summary>The vertical space around a choice.</summary>
    private const double ChoicePadding = 4;

    /// <summary>Initializes a new instance of the <see cref="ArchiveCopyWindow"/> class.</summary>
    public ArchiveCopyWindow()
    {
        InitializeComponent();
        LevelList.ItemTemplate = new FuncDataTemplate<ArchiveLevelChoice>(static (choice, _) => CreateChoice(choice));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(LevelList));
            BindSettings(disposables);
            BindRun(disposables);
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));
            _ = LevelList.Focus();
        });
    }

    /// <summary>Creates the view of one level: its name, and under it what it means.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The view.</returns>
    private static StackPanel CreateChoice(ArchiveLevelChoice? choice)
    {
        var name = new TextBlock { Text = choice?.Name, FontWeight = FontWeight.SemiBold };
        var description = new TextBlock { Text = choice?.Description, TextWrapping = TextWrapping.Wrap };
        description.Classes.Add("secondary");
        return new StackPanel { Spacing = ChoiceGap, Margin = new(0, ChoicePadding), HorizontalAlignment = HorizontalAlignment.Stretch, Children = { name, description } };
    }

    /// <summary>Binds the heading and the level choices.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindSettings(MultipleDisposable disposables)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Save a PDF/A copy of {name}"));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Levels, static v => v.LevelList.ItemsSource));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedLevel, static v => v.LevelList.SelectedItem, static choice => choice, static item => item as ArchiveLevelChoice));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.LevelList.IsEnabled, static running => !running));
    }

    /// <summary>Binds the save, cancel and close buttons, the busy bar, the summary and the save picker.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindRun(MultipleDisposable disposables)
    {
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));

        // A separate busy bar shows while running: switching one bar out of indeterminate mode can leave its moving
        // segment drawn when the value stays at zero, as after a refusal.
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.BusyProgress.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.CopyProgress.IsVisible, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.CopyProgress.Value));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
    }

    /// <summary>Asks where to save the copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save PDF/A Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
