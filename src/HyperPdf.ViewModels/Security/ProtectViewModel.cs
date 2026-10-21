// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Protection;
using ReactiveUI.Binding;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Security;

/// <summary>
/// The "Protect with password" window: chooses passwords, what readers may do and the encryption, then saves a protected
/// copy. Passwords live only in this view model while the window is open; they are never logged, saved or shown, and are
/// cleared when the window closes.
/// </summary>
[DebuggerDisplay("ProtectViewModel: {FileName}")]
public sealed partial class ProtectViewModel : ProtectionRunViewModel
{
    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose Save Protected Copy to make the file.";

    /// <summary>The protector the open document provides.</summary>
    private readonly IDocumentProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="ProtectViewModel"/> class.</summary>
    /// <param name="protector">The open document's protector.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    public ProtectViewModel(IDocumentProtector protector, string sourcePath)
        : base(sourcePath)
    {
        ArgumentNullException.ThrowIfNull(protector);
        _protector = protector;
        Status = protector.GetStatus();
        StatusText = ReadyStatus;
        SelectedFormat = ProtectionChecks.Formats[0];
        _accessibilityWarningHelper = this.WhenChanged(static vm => vm.AllowAccessibility, static vm => vm.SelectedFormat, ProtectionChecks.AccessibilityWarning)
            .ToProperty(this, static vm => vm.AccessibilityWarning);
        Subscriptions.Add(_accessibilityWarningHelper);

        // The passwords are forgotten once the window closes.
        Subscriptions.Add(Scope.Create(ForgetPasswords));
    }

    /// <summary>Gets how the open document is protected now.</summary>
    public ProtectionStatus Status { get; }

    /// <summary>Gets a value indicating whether the owner password of the current protection is needed to change it.</summary>
    public bool NeedsCurrentOwner => Status is { IsProtected: true, CanChange: false, UsesCertificates: false };

    /// <summary>Gets the encryption choices.</summary>
    public IReadOnlyList<ProtectionFormatChoice> Formats => ProtectionChecks.Formats;

    /// <inheritdoc/>
    public override string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (protected).pdf";

    /// <summary>Gets or sets the encryption choice.</summary>
    [Reactive]
    public partial ProtectionFormatChoice? SelectedFormat { get; set; }

    /// <summary>Gets or sets the password that opens the copy with the permissions below.</summary>
    [Reactive]
    public partial string UserPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the reading password typed again.</summary>
    [Reactive]
    public partial string UserPasswordRepeat { get; set; } = string.Empty;

    /// <summary>Gets or sets the password that opens the copy with every permission.</summary>
    [Reactive]
    public partial string OwnerPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the owner password typed again.</summary>
    [Reactive]
    public partial string OwnerPasswordRepeat { get; set; } = string.Empty;

    /// <summary>Gets or sets the owner password of the protection the open document has now.</summary>
    [Reactive]
    public partial string CurrentOwnerPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether readers may print.</summary>
    [Reactive]
    public partial bool AllowPrint { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether readers may copy text and pictures.</summary>
    [Reactive]
    public partial bool AllowCopy { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether readers may change the pages and their content.</summary>
    [Reactive]
    public partial bool AllowEdit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether readers may fill in forms and add comments.</summary>
    [Reactive]
    public partial bool AllowForms { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether screen readers may read the text.</summary>
    [Reactive]
    public partial bool AllowAccessibility { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the title and author are hidden too.</summary>
    [Reactive]
    public partial bool EncryptMetadata { get; set; } = true;

    /// <summary>Gets the warning shown when screen readers are blocked, or empty.</summary>
    [ObservableAsProperty]
    public partial string AccessibilityWarning { get; }

    /// <summary>Clears the passwords typed, so they are not kept after the window closes.</summary>
    private void ForgetPasswords()
    {
        UserPassword = string.Empty;
        UserPasswordRepeat = string.Empty;
        OwnerPassword = string.Empty;
        OwnerPasswordRepeat = string.Empty;
        CurrentOwnerPassword = string.Empty;
    }

    /// <summary>Builds the request from the choices.</summary>
    /// <returns>The request.</returns>
    private ProtectionRequest CreateRequest()
    {
        var permissions = ProtectionPermissions.None;
        permissions |= AllowPrint ? ProtectionPermissions.Print : 0;
        permissions |= AllowCopy ? ProtectionPermissions.Copy : 0;
        permissions |= AllowEdit ? ProtectionPermissions.Edit : 0;
        permissions |= AllowForms ? ProtectionPermissions.Forms : 0;
        permissions |= AllowAccessibility ? ProtectionPermissions.Accessibility : 0;
        return new()
        {
            Format = SelectedFormat?.Format ?? ProtectionFormat.Strong,
            UserPassword = UserPassword,
            OwnerPassword = OwnerPassword,
            Permissions = permissions,
            EncryptMetadata = EncryptMetadata,
            CurrentOwnerPassword = CurrentOwnerPassword.Length == 0 ? null : CurrentOwnerPassword,
        };
    }

    /// <summary>Checks the choices, asks where to save, then writes the protected copy there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(CanStart))]
    private async Task SaveAsync()
    {
        Error = null;
        var request = CreateRequest();
        if (ProtectionChecks.Problem(request, UserPasswordRepeat, OwnerPasswordRepeat) is { } problem)
        {
            Error = problem;
            return;
        }

        await RunAsync(
            SuggestedName,
            "Protecting the copy. This can take a while for long files.",
            "the protection",
            (stream, token) => _protector.ProtectAsync(request, stream, token)).ConfigureAwait(true);
    }
}
