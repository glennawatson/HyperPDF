// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Checks that a save marked <see cref="PdfCompactOptions.Reproducible"/> can keep its promise. The writers pick no
/// random value of their own except the initialisation vector of AES encryption (ISO 32000-2 §7.6.3), which makes an
/// AES-encrypted file differ on every save.
/// </summary>
internal static class PdfReproducibility
{
    /// <summary>The message of the failure when a reproducible save would encrypt with AES.</summary>
    private const string AesMessage =
        "Reproducible output cannot be encrypted with AES, which picks a random initialisation vector for each string and stream. "
        + "Save it decrypted, or leave reproducible output off.";

    /// <summary>The message of the failure when a reproducible save would need a PDF MAC.</summary>
    private const string MacMessage =
        "Reproducible output cannot keep this document's PDF MAC (ISO/TS 32004): a PDF MAC document is encrypted with AES, which picks random values, "
        + "and each token uses a new random key. Save it decrypted, or leave reproducible output off.";

    /// <summary>Fails when the save is asked to be reproducible but would encrypt with AES.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="options">The layout.</param>
    /// <exception cref="PdfException">The output would be encrypted with AES, which uses random initialisation vectors.</exception>
    internal static void ThrowIfNotReproducible(PdfObjectStore store, in PdfCompactOptions options)
    {
        if (options.Reproducible && options.Encryption is not null)
        {
            // New encryption draws a new file key, salts and initialisation vectors.
            throw new PdfException(PdfError.Security, AesMessage);
        }

        if (options.Reproducible && !options.RemoveEncryption && PdfMacSeal.IsNeeded(store))
        {
            throw new PdfException(PdfError.Security, MacMessage);
        }

        if (options.Reproducible && !options.RemoveEncryption && store.Security is { UsesRandomIv: true })
        {
            throw new PdfException(PdfError.Security, AesMessage);
        }
    }
}
