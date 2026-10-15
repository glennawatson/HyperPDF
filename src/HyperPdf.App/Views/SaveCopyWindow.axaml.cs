// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses the format and settings for a copy of the open document and writes it. The progress bar, summary and buttons
/// stay fixed at the bottom so nothing moves while a copy is made.
/// </summary>
[DebuggerDisplay("SaveCopyWindow: {Title}")]
public sealed partial class SaveCopyWindow : ReactiveUI.Avalonia.ReactiveWindow<SaveCopyViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="SaveCopyWindow"/> class.</summary>
    public SaveCopyWindow()
    {
        InitializeComponent();
        FormatList.ItemTemplate = new FuncDataTemplate<SaveCopyFormatChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        PresetList.ItemTemplate = new FuncDataTemplate<OptimizePresetChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        LevelList.ItemTemplate = new FuncDataTemplate<ArchiveLevelChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        ScopeList.ItemTemplate = new FuncDataTemplate<FlattenScopeChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        LookList.ItemTemplate = new FuncDataTemplate<FlattenLookChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        FieldLabels.Link((LanguageBox, LanguageLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(FormatList));
            disposables.Add(ItemAutomation.NameItems(PresetList));
            disposables.Add(ItemAutomation.NameItems(LevelList));
            disposables.Add(ItemAutomation.NameItems(ScopeList));
            disposables.Add(ItemAutomation.NameItems(LookList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Save a copy of {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Formats, static v => v.FormatList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedFormat, static v => v.FormatList.SelectedItem, static choice => choice, static item => item as SaveCopyFormatChoice));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.FormatList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsStandardFormat, static v => v.StandardPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsOptimizeFormat, static v => v.OptimizePanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsArchiveFormat, static v => v.ArchivePanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsPdf20Format, static v => v.Pdf20Panel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsFlattenFormat, static v => v.FlattenPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Optimize.Presets, static v => v.PresetList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Optimize.SelectedPreset, static v => v.PresetList.SelectedItem, static choice => choice, static item => item as OptimizePresetChoice));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Optimize.FixAccessibility, static v => v.FixBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Optimize.Language, static v => v.LanguageBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Optimize.AddInferredTags, static v => v.TagsBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Optimize.CleanUp, static v => v.CleanBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.OptimizePanel.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Optimize.FixAccessibility, static v => v.LanguageBox.IsEnabled));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Archive.Levels, static v => v.LevelList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Archive.SelectedLevel, static v => v.LevelList.SelectedItem, static choice => choice, static item => item as ArchiveLevelChoice));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ArchivePanel.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Flatten.Scopes, static v => v.ScopeList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Flatten.SelectedScope, static v => v.ScopeList.SelectedItem, static choice => choice, static item => item as FlattenScopeChoice));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Flatten.Looks, static v => v.LookList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Flatten.SelectedLook, static v => v.LookList.SelectedItem, static choice => choice, static item => item as FlattenLookChoice));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Flatten.TurnPagesUpright, static v => v.UprightBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.FlattenPanel.IsEnabled, static running => !running));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsBusy, static v => v.BusyProgress.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsBusy, static v => v.CopyProgress.IsVisible, static busy => !busy));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.CopyProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand).SwitchMap(static closed => closed).SubscribeSafe(_ => Close(), ErrorTrace.Write));
            _ = FormatList.Focus();
        });
    }

    /// <summary>Asks where to save the copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save a Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
