// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>
/// What a person who opens an encrypted document with the user password (or as a recipient) may do. The values are the
/// bits of the standard security handler's <c>/P</c> (ISO 32000-2 7.6.4.2, Table 22) and of the public-key permissions
/// (7.6.5.2, Table 24); a set flag allows the action.
/// </summary>
[Flags]
public enum PdfAccessPermissions
{
    /// <summary>Nothing is allowed.</summary>
    None = 0,

    /// <summary>
    /// Bit 2: permits changing the encryption and enables all other permissions. Only public-key recipients use it; it is
    /// the owner-like right of a recipient list.
    /// </summary>
    ChangeEncryption = 1 << 1,

    /// <summary>Bit 3: print the document, possibly not at the highest quality.</summary>
    Print = 1 << 2,

    /// <summary>Bit 4: modify the contents of the document by operations other than annotating, filling forms and assembling.</summary>
    ModifyContent = 1 << 3,

    /// <summary>Bit 5: copy or extract text and graphics.</summary>
    CopyContent = 1 << 4,

    /// <summary>Bit 6: add or change annotations and fill in form fields.</summary>
    ModifyAnnotations = 1 << 5,

    /// <summary>Bit 9: fill in existing form fields, even when <see cref="ModifyAnnotations"/> is clear.</summary>
    FillForms = 1 << 8,

    /// <summary>
    /// Bit 10: extract content for accessibility tools. ISO 32000-2 no longer uses this bit and says writers always set it,
    /// so a PDF 2.0 file keeps it set. It can still be cleared in an AES-128 file, which follows ISO 32000-1.
    /// </summary>
    ExtractForAccessibility = 1 << 9,

    /// <summary>Bit 11: assemble the document (insert, rotate or delete pages, and add outline items or thumbnails).</summary>
    AssemblePages = 1 << 10,

    /// <summary>Bit 12: print at the highest quality. Without it printing is limited to a low-quality copy. It only matters when <see cref="Print"/> is set.</summary>
    PrintHighQuality = 1 << 11,

    /// <summary>Everything a user may be granted: all the flags above except <see cref="ChangeEncryption"/>.</summary>
    All = Print | ModifyContent | CopyContent | ModifyAnnotations | FillForms | ExtractForAccessibility | AssemblePages | PrintHighQuality,
}
