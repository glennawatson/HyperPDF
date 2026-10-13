// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The outcome of opening an encrypted document.</summary>
public enum PdfSecurityResult
{
    /// <summary>The password was accepted.</summary>
    Success = 0,

    /// <summary>The password was wrong, or a password is needed.</summary>
    WrongPassword = 1,

    /// <summary>The document uses a security handler other than the standard one.</summary>
    UnsupportedHandler = 2,

    /// <summary>The document is encrypted for certificates, and no certificate given can open it.</summary>
    CertificateRequired = 3,

    /// <summary>The encryption dictionary is damaged, for example a missing or oversized /Recipients.</summary>
    Damaged = 4,

    /// <summary>A recipient is for the certificate, but the certificate has no private key.</summary>
    PrivateKeyRequired = 5,

    /// <summary>
    /// A recipient is for the certificate, but it uses a key type or CMS algorithm that cannot be decrypted, such as an
    /// unsupported key derivation, key wrap or content cipher.
    /// </summary>
    UnsupportedRecipient = 6,

    /// <summary>
    /// A recipient's CMS data is damaged: it cannot be read, or the recipient for the certificate does not decrypt with
    /// its private key.
    /// </summary>
    RecipientDamaged = 7,

    /// <summary>The document uses AES-GCM (ISO/TS 32003), and this computer's cryptography library does not provide it.</summary>
    UnsupportedCipher = 8,
}
