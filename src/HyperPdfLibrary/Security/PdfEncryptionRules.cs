// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Security;

/// <summary>The conditions under which a file is encrypted, or the plain reason it is not.</summary>
internal static class PdfEncryptionRules
{
    /// <summary>Checks that a document may be encrypted with the options, before anything is written.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The save options holding <see cref="PdfCompactOptions.Encryption"/>.</param>
    /// <param name="encryption">The encryption options.</param>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    /// <exception cref="PdfException">The document cannot be encrypted, with the reason in plain words.</exception>
    internal static void Check(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionOptions encryption)
    {
        if (options.RemoveEncryption)
        {
            throw new ArgumentException("A save cannot both encrypt the file and remove its encryption.", nameof(options));
        }

        CheckOptions(encryption);
        if (SignaturePresence.HasSignature(store))
        {
            throw new PdfException(PdfError.Security, PdfProtectionMessages.Signed);
        }

        if (PdfAClaimGuard.IsClaimed(store))
        {
            throw new PdfException(PdfError.Security, PdfProtectionMessages.PdfA);
        }

        // PDF/UA-1 needs the accessibility permission bit set in an encrypted file (ISO 14289-1:2014, 7.16). Only AES-128 can clear it.
        if (PdfUaClaimGuard.IsPart1Claimed(store) && encryption.Recipients is null && encryption.Algorithm == PdfEncryptionAlgorithm.Aes128
            && (encryption.Permissions & PdfAccessPermissions.ExtractForAccessibility) == 0)
        {
            throw new PdfException(PdfError.ClaimUnmet, PdfProtectionMessages.PdfUa);
        }

        CheckAuthority(store, options);
    }

    /// <summary>Checks the options on their own.</summary>
    /// <param name="encryption">The encryption options.</param>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    /// <exception cref="PdfException">The cipher is not available on this computer.</exception>
    internal static void CheckOptions(PdfEncryptionOptions encryption)
    {
        if (encryption.Algorithm is not (PdfEncryptionAlgorithm.Aes256 or PdfEncryptionAlgorithm.Aes128 or PdfEncryptionAlgorithm.Aes256Gcm))
        {
            throw new ArgumentException("The encryption algorithm is not one this library writes.", nameof(encryption));
        }

        if (encryption.Algorithm == PdfEncryptionAlgorithm.Aes256Gcm && !AesGcmCipher.IsSupported)
        {
            throw new PdfException(PdfError.Security, "AES-GCM encryption (ISO/TS 32003) is not available in this computer's cryptography library. Choose AES-256 instead.");
        }

        if (encryption.Recipients is null)
        {
            CheckPasswords(encryption);
            return;
        }

        CheckRecipients(encryption);
    }

    /// <summary>Checks a public-key file's options.</summary>
    /// <param name="encryption">The encryption options.</param>
    /// <exception cref="ArgumentException">The options have no recipients, mix passwords with recipients, or name an algorithm public-key files cannot use.</exception>
    private static void CheckRecipients(PdfEncryptionOptions encryption)
    {
        if (encryption.Recipients is not { Count: > 0 })
        {
            throw new ArgumentException("A public-key file needs at least one recipient.", nameof(encryption));
        }

        if (!string.IsNullOrEmpty(encryption.UserPassword) || !string.IsNullOrEmpty(encryption.OwnerPassword))
        {
            throw new ArgumentException("A file is encrypted with passwords or for recipients, not both.", nameof(encryption));
        }

        if (encryption.Algorithm == PdfEncryptionAlgorithm.Aes256Gcm)
        {
            throw new ArgumentException("Public-key files use AES-128 or AES-256; AES-GCM is only for password files.", nameof(encryption));
        }

        foreach (var recipient in encryption.Recipients)
        {
            ArgumentNullException.ThrowIfNull(recipient);
            ArgumentNullException.ThrowIfNull(recipient.Certificate);
        }
    }

    /// <summary>Checks that a password file's restrictions can be enforced: a restricted file needs an owner password that differs from the user password.</summary>
    /// <param name="encryption">The encryption options.</param>
    /// <exception cref="ArgumentException">The file is restricted but has no separate owner password.</exception>
    private static void CheckPasswords(PdfEncryptionOptions encryption)
    {
        var owner = encryption.OwnerPassword;
        var separateOwner = !string.IsNullOrEmpty(owner) && !string.Equals(owner, encryption.UserPassword, StringComparison.Ordinal);
        if (!separateOwner && !PdfPermissionBits.AllowsEverything(PdfPermissionBits.ToStandard(encryption.Permissions, true)))
        {
            throw new ArgumentException(
                "Restricting what a reader may do needs an owner password that is different from the user password. Without one, the user password would also give every permission.",
                nameof(encryption));
        }
    }

    /// <summary>
    /// Checks that whoever opened an already protected document may replace its protection (ISO 32000-2 7.6.4): the owner may; a
    /// user may only when the document already allows everything.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The save options.</param>
    /// <exception cref="PdfException">The document was opened as a user and is restricted.</exception>
    private static void CheckAuthority(PdfObjectStore store, in PdfCompactOptions options)
    {
        if (store.Security is not { } security || options.OwnerVerified || security.Role == PdfSecurityRole.Owner
            || PdfPermissionBits.AllowsEverything(security.Permissions))
        {
            return;
        }

        throw new PdfException(PdfError.Security, PdfProtectionMessages.NeedsOwner);
    }
}
