// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>
/// Applies the redaction marks and writes the redacted copy. The warning, the confirmation, the progress and the summary
/// are always on screen, so the layout does not move while the copy is written; the settings are disabled during the run.
/// </summary>
[DebuggerDisplay("RedactionWindow: {Title}")]
public sealed partial class RedactionWindow : ReactiveUI.Avalonia.ReactiveWindow<RedactionViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="RedactionWindow"/> class.</summary>
    public RedactionWindow()
    {
        InitializeComponent();
        ImageList.ItemTemplate = new FuncDataTemplate<RedactionImageOption>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        LineArtList.ItemTemplate = new FuncDataTemplate<RedactionLineArtOption>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(ImageList));
            disposables.Add(ItemAutomation.NameItems(LineArtList));
            BindSettings(disposables);
            BindRun(disposables);
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));
            _ = UnderstandBox.Focus();
        });
    }

    /// <summary>Binds the heading, the warning and the settings.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindSettings(MultipleDisposable disposables)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Apply redactions to {name}"));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SourceNote, static v => v.NoteText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SourceNote, static v => v.NoteText.IsVisible, static note => note is not null));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.WarningText, static v => v.WarningText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ImageOptions, static v => v.ImageList.ItemsSource));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.LineArtOptions, static v => v.LineArtList.ItemsSource));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedImage, static v => v.ImageList.SelectedItem, static choice => choice, static item => item as RedactionImageOption));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedLineArt, static v => v.LineArtList.SelectedItem, static choice => choice, static item => item as RedactionLineArtOption));
        disposables.Add(this.Bind(ViewModel, static vm => vm.RemoveHiddenText, static v => v.HiddenTextBox.IsChecked, static on => on, static on => on == true));
        disposables.Add(this.Bind(ViewModel, static vm => vm.RemoveLinksAndComments, static v => v.LinksBox.IsChecked, static on => on, static on => on == true));
        disposables.Add(this.Bind(ViewModel, static vm => vm.ScrubMetadata, static v => v.MetadataBox.IsChecked, static on => on, static on => on == true));
        disposables.Add(this.Bind(ViewModel, static vm => vm.Understands, static v => v.UnderstandBox.IsChecked, static on => on, static on => on == true));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ImageList.IsEnabled, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.LineArtList.IsEnabled, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.HiddenTextBox.IsEnabled, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.LinksBox.IsEnabled, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.MetadataBox.IsEnabled, static running => !running));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.UnderstandBox.IsEnabled, static running => !running));
    }

    /// <summary>Binds the apply, cancel and close buttons, the progress, the summary and the save picker.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindRun(MultipleDisposable disposables)
    {
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.ApplyCommand, static v => v.ApplyButton));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.RunProgress.IsIndeterminate));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
        disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
    }

    /// <summary>Asks where to save the redacted copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Redacted Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
