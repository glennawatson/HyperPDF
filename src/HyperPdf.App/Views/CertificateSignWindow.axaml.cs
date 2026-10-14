// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Settings;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Asks for a certificate and its password to sign with. Closes with <see langword="true"/> to sign.</summary>
[DebuggerDisplay("CertificateSignWindow: {Title}")]
public sealed partial class CertificateSignWindow : ReactiveUI.Avalonia.ReactiveWindow<CertificateSignViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="CertificateSignWindow"/> class.</summary>
    public CertificateSignWindow()
    {
        InitializeComponent();
        RememberedBox.ItemTemplate = new FuncDataTemplate<RememberedCertificate>(static (certificate, _) => new TextBlock { Text = ItemAutomation.DescribeCertificate(certificate) });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(ItemAutomation.NameItems(RememberedBox));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Remembered, static v => v.RememberedBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedCertificate, static v => v.RememberedBox.SelectedItem, static chosen => chosen, static item => item as RememberedCertificate));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasRemembered, static v => v.RememberedRow.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ForgetCommand, static v => v.ForgetButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Remember, static v => v.RememberCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CertificatePath, static v => v.CertificateBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Password, static v => v.PasswordBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.BrowseCommand, static v => v.BrowseButton));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.BrowseInteraction, BrowseAsync));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Reason, static v => v.ReasonBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Location, static v => v.LocationBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CertificationChoices, static v => v.KindBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CertificationIndex, static v => v.KindBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsVisibleSignature, static v => v.VisibleCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.PreviewText, static v => v.PreviewText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasPreview, static v => v.PreviewPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.IsVisible, static error => error is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SignCommand, static v => v.SignButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(answer => Close(answer), static error => Trace.TraceError(error.ToString())));

            // The named boxes are the state each chain needs, and a control cannot be passed through the operators, so these
            // lambdas read the window's own fields. Focus starts in the password when a certificate is already named.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CertificatePath)
                .Take(1)
                .Select(path => string.IsNullOrEmpty(path) ? CertificateBox : PasswordBox)
                .SubscribeSafe(static box => box.Focus(), static error => Trace.TraceError(error.ToString())));

            // After a wrong password, the password box takes the keyboard again with its text selected, ready to retype.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Error)
                .Where(static error => error is not null)
                .Select(_ => PasswordBox)
                .SubscribeSafe(
                    static box =>
                    {
                        _ = box.Focus();
                        box.SelectAll();
                    },
                    static error => Trace.TraceError(error.ToString())));
        });
    }

    /// <summary>Asks for a certificate file through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task BrowseAsync(IInteractionContext<RxVoid, string?> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose a Certificate",
            AllowMultiple = false,
            FileTypeFilter = [new("Certificates (.p12, .pfx)") { Patterns = ["*.p12", "*.pfx"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
