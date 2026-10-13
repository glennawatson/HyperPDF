// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>A signed, timestamped or updated file and what was done to make it.</summary>
/// <param name="Bytes">The whole file: the original bytes followed by the incremental update.</param>
/// <param name="FieldName">The fully qualified name of the signature or document timestamp field, or an empty string for a long-term validation update.</param>
/// <param name="Format">The format of the signature written, or <see cref="PdfSignatureFormat.Unknown"/> when no signature was written.</param>
/// <param name="Digest">The digest of the signature, or <see cref="PdfDigestMethod.Unknown"/> when no signature was written.</param>
/// <param name="ContentsCapacity">The room reserved for the signature value in bytes; zero when no signature was written.</param>
/// <param name="ContentsUsed">The bytes of the signature value written; zero when no signature was written.</param>
/// <param name="Attempts">The times the file was built; more than one when the room reserved was too small for the finished signature.</param>
/// <param name="Notes">What the user should know, in plain sentences (for example a PDF version raised to 2.0).</param>
[DebuggerDisplay("PdfSigningResult: {FieldName} {ContentsUsed}/{ContentsCapacity} bytes")]
public sealed record PdfSigningResult(
    byte[] Bytes,
    string FieldName,
    PdfSignatureFormat Format,
    PdfDigestMethod Digest,
    int ContentsCapacity,
    int ContentsUsed,
    int Attempts,
    IReadOnlyList<string> Notes);
