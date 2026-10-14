// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The panels that cover the pages when a document could not be opened, or needs a password or a certificate.</summary>
[DebuggerDisplay("DocumentLockView: {ViewModel}")]
public sealed partial class DocumentLockView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="DocumentLockView"/> class.</summary>
    public DocumentLockView()
    {
        InitializeComponent();
        FieldLabels.Link((OpenCertificatePathBox, OpenCertificateLabel), (OpenCertificatePasswordBox, OpenCertificatePasswordLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorPanel.IsVisible, static message => message is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsPassword, static v => v.PasswordPanel.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PasswordEntry, static v => v.PasswordBox.Text, static entry => entry, static text => text ?? string.Empty));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SubmitPasswordCommand, static v => v.UnlockButton));
            disposables.Add(PasswordBox.KeyDowns.Enter.AsVoid.InvokeCommand(this, static v => v.ViewModel!.SubmitPasswordCommand));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsCertificate, static v => v.CertificatePanel.IsVisible));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CertificatePathEntry, static v => v.OpenCertificatePathBox.Text, static entry => entry, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CertificatePasswordEntry, static v => v.OpenCertificatePasswordBox.Text, static entry => entry, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CertificateError, static v => v.OpenCertificateErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CertificateError, static v => v.OpenCertificateErrorText.IsVisible, static error => error is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.BrowseCertificateCommand, static v => v.BrowseOpenCertificateButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SubmitCertificateCommand, static v => v.UnlockCertificateButton));
            disposables.Add(OpenCertificatePasswordBox.KeyDowns.Enter.AsVoid.InvokeCommand(this, static v => v.ViewModel!.SubmitCertificateCommand));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CertificateBrowseInteraction).HandleWith(BrowseCertificateAsync));
        });
    }

    /// <summary>Asks the desktop for a local PKCS #12 certificate file.</summary>
    /// <param name="context">The certificate path request.</param>
    /// <returns>A task for the file picker.</returns>
    private async Task BrowseCertificateAsync(IInteractionContext<RxVoid, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not { } owner)
        {
            context.SetOutput(null);
            return;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose a Certificate",
            AllowMultiple = false,
            FileTypeFilter = [new("Certificates (.p12, .pfx)") { Patterns = ["*.p12", "*.pfx"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
