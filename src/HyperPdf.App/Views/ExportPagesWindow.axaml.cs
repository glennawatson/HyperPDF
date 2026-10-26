// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.Core.Exporting;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses a file format and pages and exports them as picture files or drawings. Every section is always on screen, so
/// the layout does not move during the export; the choices are disabled during the run and the summary fills in when it ends.
/// </summary>
[DebuggerDisplay("ExportPagesWindow: {Title}")]
public sealed partial class ExportPagesWindow : ReactiveUI.Avalonia.ReactiveWindow<ExportPagesViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ExportPagesWindow"/> class.</summary>
    public ExportPagesWindow()
    {
        InitializeComponent();
        FormatList.ItemTemplate = new FuncDataTemplate<PageFileFormatInfo>(
            static (format, _) => ChoiceViews.Create(format, static item => item?.Name, static item => item?.Description));
        ScopeList.ItemTemplate = new FuncDataTemplate<ExportPagesScopeChoice>(
            static (choice, _) => ChoiceViews.Create(choice, static item => item?.Name, static _ => null));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(UiText.CultureChanges.SubscribeSafe(_ => RefreshCultureText(), ErrorTrace.Write));
            disposables.Add(ItemAutomation.NameItems(FormatList));
            disposables.Add(ItemAutomation.NameItems(ScopeList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => UiText.Format("Export pages of {0}", name)));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Formats, static v => v.FormatList.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.SelectedFormat,
                static v => v.FormatList.SelectedItem,
                static format => format,
                static item => item as PageFileFormatInfo ?? PageFileFormats.All[0]));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Scopes, static v => v.ScopeList.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.SelectedScope,
                static v => v.ScopeList.SelectedItem,
                static choice => choice,
                static item => item as ExportPagesScopeChoice ?? new(ExportPagesScopeKind.CurrentPage, string.Empty)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PagesText, static v => v.PagesBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Dpi, static v => v.DpiBox.Value, static dpi => (decimal?)dpi, static value => (int)(value ?? PageFileExportOptions.DefaultDpi)));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IncludeMarkup, static v => v.MarkupBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.FormatList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ScopeList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.PagesBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.DpiBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.MarkupBox.IsEnabled, static running => !running));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));

            // A separate busy bar shows while running: switching one bar out of indeterminate mode can leave its moving
            // segment drawn when the value stays at zero, as after a refusal.
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.BusyProgress.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ExportProgress.IsVisible, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.ExportProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));
            _ = FormatList.Focus();
        });
    }

    /// <summary>Asks where to save the export, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = UiText.Get("Export Pages"), SuggestedFileName = context.Input });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Refreshes translated presentation text without changing export choices.</summary>
    private void RefreshCultureText()
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        HeadingText.Text = UiText.Format("Export pages of {0}", model.FileName);
    }
}
