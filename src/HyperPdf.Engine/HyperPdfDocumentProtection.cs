// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Protection;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;

namespace HyperPdf.Engine;

/// <summary>Writes protected and unprotected copies of the open document with the library.</summary>
internal static class HyperPdfDocumentProtection
{
    /// <summary>Describes the document's protection.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The status.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static ProtectionStatus GetStatus(HyperPdfDocument self)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        var info = PdfDocumentProtection.GetProtection(self.Document);
        return new(info.IsProtected, info.CanChange, info.IsPublicKey);
    }

    /// <summary>Writes a protected copy, or explains why it cannot. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="request">The passwords and permissions; never logged.</param>
    /// <param name="destination">The stream that receives the copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async Task<ProtectionResult> ProtectAsync(HyperPdfDocument self, ProtectionRequest request, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The snapshot carries every unsaved edit, so the copy matches what the user sees.
            using var snapshot =HyperPdfOptimization.TakeSnapshot(self);
            var result = await PdfDocumentProtection.ProtectAsync(snapshot, ToOptions(request), request.CurrentOwnerPassword, destination, cancellationToken).ConfigureAwait(false);
            return new(result.Refusal, result.Notes);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Writes a copy without protection, or explains why it cannot. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="ownerPassword">The owner password, or <see langword="null"/>; never logged.</param>
    /// <param name="destination">The stream that receives the copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async Task<ProtectionResult> RemoveAsync(HyperPdfDocument self, string? ownerPassword, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var result = await PdfDocumentProtection.RemoveProtectionAsync(snapshot, ownerPassword, destination, cancellationToken).ConfigureAwait(false);
            return new(result.Refusal, result.Notes);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Maps the app's plain choices to the library's options.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The options.</returns>
    private static PdfEncryptionOptions ToOptions(ProtectionRequest request) => new()
    {
        Algorithm = request.Format switch
        {
            ProtectionFormat.Compatible => PdfEncryptionAlgorithm.Aes128,
            ProtectionFormat.Authenticated => PdfEncryptionAlgorithm.Aes256Gcm,
            _ => PdfEncryptionAlgorithm.Aes256,
        },
        UserPassword = request.UserPassword,
        OwnerPassword = request.OwnerPassword,
        Permissions = ToPermissions(request.Permissions),
        EncryptMetadata = request.EncryptMetadata,
    };

    /// <summary>Maps the plain permission choices to the standard's bits.</summary>
    /// <param name="permissions">The choices.</param>
    /// <returns>The library flags.</returns>
    private static PdfAccessPermissions ToPermissions(ProtectionPermissions permissions)
    {
        var result = PdfAccessPermissions.None;
        result |= (permissions & ProtectionPermissions.Print) != 0 ? PdfAccessPermissions.Print | PdfAccessPermissions.PrintHighQuality : 0;
        result |= (permissions & ProtectionPermissions.Copy) != 0 ? PdfAccessPermissions.CopyContent : 0;
        result |= (permissions & ProtectionPermissions.Edit) != 0 ? PdfAccessPermissions.ModifyContent | PdfAccessPermissions.AssemblePages : 0;
        result |= (permissions & ProtectionPermissions.Forms) != 0 ? PdfAccessPermissions.ModifyAnnotations | PdfAccessPermissions.FillForms : 0;
        result |= (permissions & ProtectionPermissions.Accessibility) != 0 ? PdfAccessPermissions.ExtractForAccessibility : 0;
        return result;
    }
}
