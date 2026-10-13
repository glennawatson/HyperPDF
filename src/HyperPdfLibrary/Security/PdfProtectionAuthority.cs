// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>Decides who may change or remove a document's protection (ISO 32000-2 7.6.4): the owner, or anyone when the document already allows everything.</summary>
internal static class PdfProtectionAuthority
{
    /// <summary>Describes a document's protection.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description.</returns>
    internal static PdfProtectionInfo Describe(PdfDocument document)
    {
        if (document.Objects.Security is not { } security)
        {
            return new(false, false, null, 0, PdfAccessPermissions.All, true, PdfSecurityRole.Owner, true);
        }

        var publicKey = PdfSecurityHandler.IsPublicKey(document.Objects.Trailer.GetDictionary(KnownName.Encrypt) ?? new PdfDictionary(null));
        return new(
            true,
            publicKey,
            AlgorithmOf(security),
            security.Revision,
            PdfPermissionBits.FromWord(security.Permissions),
            security.EncryptMetadata,
            security.Role,
            security.Role == PdfSecurityRole.Owner || PdfPermissionBits.AllowsEverything(security.Permissions));
    }

    /// <summary>Determines whether the protection may be changed, given the owner password the person typed, if any.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ownerPassword">The owner password, or <see langword="null"/> when none was given.</param>
    /// <returns><see langword="true"/> when the document is not protected, the person opened it as the owner or it allows everything, or the password is the owner password.</returns>
    internal static bool IsAuthorised(PdfDocument document, string? ownerPassword)
    {
        if (Describe(document).CanChange)
        {
            return true;
        }

        // A public-key file has no owner password; its owner is a recipient with the right to change the encryption.
        var encrypt = document.Objects.Trailer.GetDictionary(KnownName.Encrypt);
        if (ownerPassword is null || encrypt is null || PdfSecurityHandler.IsPublicKey(encrypt))
        {
            return false;
        }

        var firstId = document.Objects.Trailer.GetArray(KnownName.ID) is { } ids ? ids.Get(0).AsStringBytes() : [];
        var result = PdfSecurityHandler.TryCreate(encrypt, firstId, ownerPassword, out var handler);
        using (handler)
        {
            return result == PdfSecurityResult.Success && handler!.Role == PdfSecurityRole.Owner;
        }
    }

    /// <summary>Gets the cipher a handler writes streams with.</summary>
    /// <param name="security">The security handler.</param>
    /// <returns>The algorithm, or <see langword="null"/> for RC4.</returns>
    private static PdfEncryptionAlgorithm? AlgorithmOf(PdfSecurityHandler security) => security.StreamMethod switch
    {
        CryptMethod.Aes128 => PdfEncryptionAlgorithm.Aes128,
        CryptMethod.Aes256 => PdfEncryptionAlgorithm.Aes256,
        CryptMethod.AesGcm => PdfEncryptionAlgorithm.Aes256Gcm,
        _ => null,
    };
}
