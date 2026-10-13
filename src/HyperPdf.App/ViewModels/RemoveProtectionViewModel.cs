// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Protection;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The "Remove protection" window: saves a copy without passwords or permission limits. A document opened with only its
/// reading password needs the owner password. The password is never logged, saved or shown, and is cleared when the window closes.
/// </summary>
[DebuggerDisplay("RemoveProtectionViewModel: {FileName}")]
public sealed partial class RemoveProtectionViewModel : ProtectionRunViewModel
{
    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose Save Unprotected Copy to make the file.";

    /// <summary>The protector the open document provides.</summary>
    private readonly IDocumentProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="RemoveProtectionViewModel"/> class.</summary>
    /// <param name="protector">The open document's protector.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    public RemoveProtectionViewModel(IDocumentProtector protector, string sourcePath)
        : base(sourcePath)
    {
        ArgumentNullException.ThrowIfNull(protector);
        _protector = protector;
        Status = protector.GetStatus();
        StatusText = ReadyStatus;
    }

    /// <summary>Gets how the open document is protected now.</summary>
    public ProtectionStatus Status { get; }

    /// <summary>Gets a value indicating whether the owner password is needed.</summary>
    public bool NeedsOwnerPassword => Status is { IsProtected: true, CanChange: false, UsesCertificates: false };

    /// <inheritdoc/>
    public override string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (unprotected).pdf";

    /// <summary>Gets or sets the owner password.</summary>
    [Reactive]
    public partial string OwnerPassword { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            OwnerPassword = string.Empty;
        }
    }

    /// <summary>Asks where to save, then writes the copy without protection there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(CanStart))]
    private async Task SaveAsync()
    {
        Error = null;
        var password = OwnerPassword.Length == 0 ? null : OwnerPassword;
        await RunAsync(
            SuggestedName,
            "Removing the protection from a copy. This can take a while for long files.",
            "removing the protection",
            (stream, token) => _protector.RemoveProtectionAsync(password, stream, token)).ConfigureAwait(true);
    }
}
