// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Security;

/// <summary>What the check of one revision's PDF MAC works from.</summary>
/// <param name="File">The whole file.</param>
/// <param name="Revisions">Every revision of the file.</param>
/// <param name="Index">The revision being checked.</param>
/// <param name="Raw">The revision's objects, read without decrypting, because the trailer, the Encrypt dictionary and signature contents are never encrypted.</param>
/// <param name="AuthCode">The revision's <c>/AuthCode</c> dictionary.</param>
/// <param name="Security">The document's security handler, which holds the file key.</param>
/// <param name="Salt">The 32-byte <c>/KDFSalt</c>.</param>
/// <param name="Names">The ISO/TS 32004 names.</param>
[DebuggerDisplay("PdfMacCheckContext: revision {Index}")]
internal sealed record PdfMacCheckContext(
    PdfByteSource File,
    PdfRevision[] Revisions,
    int Index,
    PdfObjectStore Raw,
    PdfDictionary AuthCode,
    PdfSecurityHandler Security,
    byte[] Salt,
    PdfMacNames Names)
{
    /// <summary>Gets the revision being checked.</summary>
    internal PdfRevision Revision => Revisions[Index];

    /// <summary>Gets a value indicating whether the revision is the last one, so its bytes run to the end of the file.</summary>
    internal bool IsLast => Index == Revisions.Length - 1;

    /// <summary>
    /// Determines whether the bytes covered end where the revision ends (ISO/TS 32004 6.5.1 and B.2.2.2): at its
    /// <c>%%EOF</c> marker or its line end, and for the last revision with nothing but white space after.
    /// </summary>
    /// <param name="end">The position after the last covered byte.</param>
    /// <returns><see langword="true"/> when the whole revision is covered.</returns>
    internal bool CoversRevision(long end) =>
        end >= Revision.MarkerEnd && end <= Revision.EndOffset && (!IsLast || PdfByteRangeChecker.IsBlank(File, end));
}
