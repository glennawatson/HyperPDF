// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// Saves a copy of the open document without protection. The owner password box shows only when the document was opened
/// with its reading password. Every section is always on screen, so the layout does not move while the copy is made.
/// </summary>
[DebuggerDisplay("RemoveProtectionWindow: {Title}")]
public sealed partial class RemoveProtectionWindow : ReactiveUI.Avalonia.ReactiveWindow<RemoveProtectionViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="RemoveProtectionWindow"/> class.</summary>
    public RemoveProtectionWindow()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Remove the protection from a copy of {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsOwnerPassword, static v => v.OwnerRow.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.OwnerPassword, static v => v.OwnerBox.Text, static text => text, static text => text ?? string.Empty));
            BindRun(disposables);
            BindFocus(disposables);
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));
        });
    }

    /// <summary>Starts the keyboard in the owner password when the document needs it, otherwise on the save button.</summary>
    /// <param name="disposables">Owns the subscription.</param>
    private void BindFocus(MultipleDisposable disposables)
    {
        // The named controls are the state the chain needs, and a control cannot be passed through the operators, so
        // these lambdas read the window's own fields.
        var needsOwner = this.Events().LayoutUpdated.Take(1).WithLatestFrom(this.WhenChanged(static v => v.ViewModel!.NeedsOwnerPassword), static (_, needs) => needs);
        disposables.Add(Signal.Merge(
                needsOwner.Where(static needs => needs).Select(_ => (Control)OwnerBox),
                needsOwner.Where(static needs => !needs).Select(_ => (Control)SaveButton))
            .SubscribeSafe(static control => control.Focus(), static error => Trace.TraceError(error.ToString())));
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
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Unprotected Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
