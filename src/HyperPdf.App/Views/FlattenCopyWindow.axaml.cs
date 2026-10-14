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

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses what to flatten and writes a flattened copy of the open document. Every section is always on screen, so the
/// layout does not move while the copy is made; the choices are disabled during the run and the summary fills in when it ends.
/// </summary>
[DebuggerDisplay("FlattenCopyWindow: {Title}")]
public sealed partial class FlattenCopyWindow : ReactiveUI.Avalonia.ReactiveWindow<FlattenCopyViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FlattenCopyWindow"/> class.</summary>
    public FlattenCopyWindow()
    {
        InitializeComponent();
        ScopeList.ItemTemplate = new FuncDataTemplate<FlattenScopeChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        LookList.ItemTemplate = new FuncDataTemplate<FlattenLookChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(ScopeList));
            disposables.Add(ItemAutomation.NameItems(LookList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Save a flattened copy of {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Scopes, static v => v.ScopeList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedScope, static v => v.ScopeList.SelectedItem, static choice => choice, static item => item as FlattenScopeChoice));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Looks, static v => v.LookList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedLook, static v => v.LookList.SelectedItem, static choice => choice, static item => item as FlattenLookChoice));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TurnPagesUpright, static v => v.UprightBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ScopeList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.LookList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.UprightBox.IsEnabled, static running => !running));
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
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));
            _ = ScopeList.Focus();
        });
    }

    /// <summary>Asks where to save the copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Flattened Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
