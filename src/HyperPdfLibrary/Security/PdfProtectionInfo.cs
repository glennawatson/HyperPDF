// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>What protects a document and what the person who opened it may do.</summary>
/// <param name="IsProtected">Whether the document is encrypted.</param>
/// <param name="IsPublicKey">Whether it is encrypted for certificates rather than passwords.</param>
/// <param name="Algorithm">The cipher, or <see langword="null"/> for an older cipher (RC4) this library reads but never writes.</param>
/// <param name="Revision">The security handler revision, or 0 when the document is not protected.</param>
/// <param name="Permissions">What the document allows a user to do; everything when the document is not protected.</param>
/// <param name="EncryptMetadata">Whether the metadata stream is encrypted.</param>
/// <param name="Role">Who opened the document: the owner or a user.</param>
/// <param name="CanChange">Whether the protection can be changed or removed without a further password: the owner opened it, or it already allows everything.</param>
[DebuggerDisplay("PdfProtectionInfo: protected {IsProtected}, R{Revision}, owner {Role}")]
public sealed record PdfProtectionInfo(
    bool IsProtected,
    bool IsPublicKey,
    PdfEncryptionAlgorithm? Algorithm,
    int Revision,
    PdfAccessPermissions Permissions,
    bool EncryptMetadata,
    PdfSecurityRole Role,
    bool CanChange);
