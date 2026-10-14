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
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>
/// Chooses passwords, permissions and encryption, and saves a protected copy of the open document. Every section is
/// always on screen, so the layout does not move while the copy is made. Passwords are typed in masked boxes and are
/// never written anywhere.
/// </summary>
[DebuggerDisplay("ProtectWindow: {Title}")]
public sealed partial class ProtectWindow : ReactiveUI.Avalonia.ReactiveWindow<ProtectViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ProtectWindow"/> class.</summary>
    public ProtectWindow()
    {
        InitializeComponent();
        FormatList.ItemTemplate = new FuncDataTemplate<ProtectionFormatChoice>(static (choice, _) => ChoiceViews.Create(choice?.Name, choice?.Description));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(FormatList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Protect a copy of {name}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsCurrentOwner, static v => v.CurrentOwnerRow.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CurrentOwnerPassword, static v => v.CurrentOwnerBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.UserPassword, static v => v.UserBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.UserPasswordRepeat, static v => v.UserRepeatBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.OwnerPassword, static v => v.OwnerBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.OwnerPasswordRepeat, static v => v.OwnerRepeatBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AllowPrint, static v => v.PrintCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AllowCopy, static v => v.CopyCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AllowEdit, static v => v.EditCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AllowForms, static v => v.FormsCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AllowAccessibility, static v => v.AccessibilityCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.EncryptMetadata, static v => v.MetadataCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.AccessibilityWarning, static v => v.AccessibilityWarningText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Formats, static v => v.FormatList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedFormat, static v => v.FormatList.SelectedItem, static choice => choice, static item => item as ProtectionFormatChoice));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));

            // A separate busy bar shows while running: switching one bar out of indeterminate mode can leave its moving
            // segment drawn when the value stays at zero, as after a refusal.
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.BusyProgress.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.CopyProgress.IsVisible, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.FormatList.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.CopyProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveInteraction, SaveAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));

            // The keyboard moves once the window is open, when its boxes can take it: to the current owner password when
            // the file has one, otherwise to the user password.
            disposables.Add(this.Events().LayoutUpdated.Take(1)
                .WithLatestFrom(this.WhenChanged(static v => v.ViewModel!.NeedsCurrentOwner), static (_, needs) => needs)
                .SelectWith(this, static (window, needs) => needs ? window.CurrentOwnerBox : window.UserBox)
                .SubscribeSafe(static box => box.Focus(), ErrorTrace.Write));
        });
    }

    /// <summary>Asks where to save the copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Protected Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
