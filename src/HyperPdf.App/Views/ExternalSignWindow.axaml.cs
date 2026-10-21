// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>
/// Signs a copy of the open document with a key that stays outside the app. The three steps are always on screen, so the
/// layout does not move between them: choose the signer's certificates, prepare and save the bytes to sign, then finish
/// with the signer's answer.
/// </summary>
[DebuggerDisplay("ExternalSignWindow: {Title}")]
public sealed partial class ExternalSignWindow : ReactiveUI.Avalonia.ReactiveWindow<ExternalSignViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ExternalSignWindow"/> class.</summary>
    public ExternalSignWindow()
    {
        InitializeComponent();
        KeyBox.ItemTemplate = new FuncDataTemplate<SigningKeyChoice>(static (choice, _) => new TextBlock { Text = choice?.Name });
        ResultBox.ItemTemplate = new FuncDataTemplate<ExternalResultChoice>(static (choice, _) => new TextBlock { Text = choice?.Name, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(KeyBox));
            disposables.Add(ItemAutomation.NameItems(ResultBox));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.HeadingText.Text, static name => $"Sign a copy of {name} with an external signer"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SignerText, static v => v.CertificateBox.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.KeyChoices, static v => v.KeyBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedKey, static v => v.KeyBox.SelectedItem, static choice => choice, static item => item as SigningKeyChoice));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Reason, static v => v.ReasonBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Location, static v => v.LocationBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.KeyBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ReasonBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.LocationBox.IsEnabled, static running => !running));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.BrowseButton.IsEnabled, static running => !running));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.BrowseCommand, static v => v.BrowseButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.PrepareCommand, static v => v.PrepareButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SaveToSignCommand, static v => v.SaveToSignButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.FinishCommand, static v => v.FinishButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PendingText, static v => v.PendingText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultChoices, static v => v.ResultBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedResult, static v => v.ResultBox.SelectedItem, static choice => choice, static item => item as ExternalResultChoice));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.ResultBox.IsEnabled, static running => !running));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsRunning, static v => v.CopyProgress.IsIndeterminate));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ProgressPercent, static v => v.CopyProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ResultText, static v => v.ResultText.Text));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.BrowseInteraction, BrowseAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveToSignInteraction, SaveToSignAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.OpenSignatureInteraction, OpenSignatureAsync));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.SaveCopyInteraction, SaveCopyAsync));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));

            // The keyboard moves to the first step once the first layout is done.
            disposables.Add(this.Events().LayoutUpdated.Take(1)
                .SelectWith(this, static (window, _) => window.BrowseButton)
                .SubscribeSafe(static button => button.Focus(), ErrorTrace.Write));
        });
    }

    /// <summary>Asks for the signer's certificate files through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task BrowseAsync(IInteractionContext<RxVoid, string[]?> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose the Signer's Certificates",
            AllowMultiple = true,
            FileTypeFilter = [new("Certificates (.cer, .crt, .pem)") { Patterns = ["*.cer", "*.crt", "*.pem"] }],
        });
        var paths = new List<string>(files.Count);
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } local)
            {
                paths.Add(local);
            }
        }

        context.SetOutput(paths.Count > 0 ? paths.ToArray() : null);
    }

    /// <summary>Asks where to save the bytes to sign, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveToSignAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save the Bytes to Sign", SuggestedFileName = context.Input, DefaultExtension = "bin" });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Asks for the file the signer gave back, through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task OpenSignatureAsync(IInteractionContext<RxVoid, string?> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose the Signer's Answer", AllowMultiple = false });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    /// <summary>Asks where to save the signed copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveCopyAsync(IInteractionContext<string, string?> context)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save Signed Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
