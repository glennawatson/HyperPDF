// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Writing;

/// <summary>How <see cref="PdfCompactWriter"/> lays out a document.</summary>
/// <param name="UseObjectStreams">
/// Whether to pack objects into compressed object streams with a cross-reference stream (PDF 1.5 and later); otherwise
/// every object is written plainly with a classic table. A document that claims PDF/A-1 always gets a classic table, as
/// ISO 19005-1 forbids cross-reference streams.
/// </param>
/// <param name="RemoveEncryption">Whether to write an encrypted document decrypted, without /Encrypt.</param>
[DebuggerDisplay("PdfCompactOptions: object streams {UseObjectStreams}, remove encryption {RemoveEncryption}")]
public readonly record struct PdfCompactOptions(bool UseObjectStreams, bool RemoveEncryption)
{
    /// <summary>Gets the default: object streams, encryption kept.</summary>
    public static PdfCompactOptions Default => new(true, false);

    /// <summary>Gets the layout readable by PDF 1.4 tools: classic table, no object streams, encryption kept.</summary>
    public static PdfCompactOptions Classic => new(false, false);
}
