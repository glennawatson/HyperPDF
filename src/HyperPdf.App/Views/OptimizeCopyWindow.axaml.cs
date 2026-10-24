// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses and writes an optimised copy of the open document. Every section is always on screen, so the layout does not
/// move while the copy is written; the settings are disabled during the run and the summary fills in when it ends.
/// </summary>
[DebuggerDisplay("OptimizeCopyWindow: {Title}")]
public sealed partial class OptimizeCopyWindow : ReactiveUI.Avalonia.ReactiveWindow<OptimizeCopyViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OptimizeCopyWindow"/> class.</summary>
    public OptimizeCopyWindow()
    {
        InitializeComponent();
        PresetList.ItemTemplate = new FuncDataTemplate<OptimizePresetChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        FieldLabels.Link((LanguageBox, LanguageLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(PresetList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Save an optimised copy of {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Presets, static v => v.PresetList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedPreset, static v => v.PresetList.SelectedItem, static choice => choice, static item => item as OptimizePresetChoice));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FixAccessibility, static v => v.FixBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Language, static v => v.LanguageBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AddInferredTags, static v => v.TagsBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.DescribePictures, static v => v.DescribeBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PictureDescriptionNote, static v => v.DescribeNote.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CanDescribePictures, static v => v.DescribePanel.IsVisible));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.AddInferredTags, static v => v.ViewModel!.IsRunning, static (tags, running) => tags && !running)
                .BindTo(this, static v => v.DescribeBox.IsEnabled));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CleanUp, static v => v.CleanBox.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.PresetList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.FixBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.TagsBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.CleanBox.IsEnabled, static running => !running));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.FixAccessibility, static v => v.ViewModel!.IsRunning, static (fix, running) => fix && !running)
                .BindTo(this, static v => v.LanguageBox.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.CopyProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));
            _ = PresetList.Focus();
        });
    }

    /// <summary>Asks where to save the copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Optimised Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
