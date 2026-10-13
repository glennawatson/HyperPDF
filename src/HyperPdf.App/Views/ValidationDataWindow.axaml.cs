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
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>
/// Adds long-term validation data or a document timestamp to a copy of the open document. Every section is always on
/// screen, so the layout does not move while the copy is made; the choices are disabled during the run and the summary,
/// with every note the signer gave, fills in when it ends.
/// </summary>
[DebuggerDisplay("ValidationDataWindow: {Title}")]
public sealed partial class ValidationDataWindow : ReactiveUI.Avalonia.ReactiveWindow<ValidationDataViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ValidationDataWindow"/> class.</summary>
    public ValidationDataWindow()
    {
        InitializeComponent();
        ActionList.ItemTemplate = new FuncDataTemplate<ValidationActionChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(ActionList));
            BindChoices(disposables);
            BindRun(disposables);
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));

            // The keyboard moves to the choices once the first layout is done, when the list can take it. The named list
            // is the state the chain needs, so the lambda reads the window's own field.
            disposables.Add(this.Events().LayoutUpdated.Take(1)
                .Select(_ => ActionList)
                .SubscribeSafe(static list => list.Focus(), static error => Trace.TraceError(error.ToString())));
        });
    }

    /// <summary>Binds the heading and the choices.</summary>
    /// <param name="disposables">Owns the bindings.</param>
    private void BindChoices(MultipleDisposable disposables)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Add validation data or a timestamp to a copy of {name}"));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Actions, static v => v.ActionList.ItemsSource));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedAction, static v => v.ActionList.SelectedItem, static choice => choice, static item => item as ValidationActionChoice));
        disposables.Add(this.Bind(ViewModel, static vm => vm.IncludeVri, static v => v.VriBox.IsChecked, static on => on, static on => on == true));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ActionList.IsEnabled, static running => !running));
        disposables.Add(this.WhenChanged(
                static v => v.ViewModel!.SelectedAction,
                static v => v.ViewModel!.IsRunning,
                static (choice, running) => choice?.Action == ValidationAction.AddValidationData && !running)
            .BindTo(this, static v => v.VriBox.IsEnabled));
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
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Updated Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
