// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>
/// How to encrypt a file when it is saved (ISO 32000-2 7.6). Give passwords for the standard security handler, or
/// <see cref="Recipients"/> for the public-key handler; the two cannot be mixed. Encryption applies to the whole file, so it
/// is written by the compact writer (<see cref="Writing.PdfCompactOptions.Encryption"/>), never added by an incremental save.
/// </summary>
/// <remarks>
/// The options hold passwords, so the type has no readable text form and the library never logs them.
/// </remarks>
[DebuggerDisplay("PdfEncryptionOptions: {Algorithm}")]
public sealed class PdfEncryptionOptions
{
    /// <summary>Gets the cipher. The default is AES-256 (revision 6).</summary>
    public PdfEncryptionAlgorithm Algorithm { get; init; } = PdfEncryptionAlgorithm.Aes256;

    /// <summary>Gets the password that opens the file with the permissions in <see cref="Permissions"/>; none or empty opens it without asking.</summary>
    public string? UserPassword { get; init; }

    /// <summary>
    /// Gets the password that opens the file with every permission and may change or remove the protection. When it is
    /// none or empty the user password is used, so the file has no separate owner, and <see cref="Permissions"/> must allow everything.
    /// </summary>
    public string? OwnerPassword { get; init; }

    /// <summary>Gets what the user password allows. Everything by default.</summary>
    public PdfAccessPermissions Permissions { get; init; } = PdfAccessPermissions.All;

    /// <summary>
    /// Gets the recipients of a public-key encrypted file, or none to use passwords. Each recipient opens the file with their
    /// certificate's private key. RSA (key transport) and elliptic curve (ECDH key agreement) certificates are supported, with
    /// AES-128 or AES-256; recipients with the same permissions share one recipient list. Passwords cannot be mixed in.
    /// </summary>
    public IReadOnlyList<PdfEncryptionRecipient>? Recipients { get; init; }

    /// <summary>
    /// Gets a value indicating whether the document metadata stream is encrypted; <see langword="true"/> by default. When it
    /// is not, tools can still read the title and author without a password.
    /// </summary>
    public bool EncryptMetadata { get; init; } = true;

    /// <summary>Gets the random source; tests set a fixed one so keys and salts repeat. The default is the system generator.</summary>
    internal FillBytes? RandomFill { get; init; }
}
