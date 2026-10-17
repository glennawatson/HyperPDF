// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>Holds a prepared incremental update and its reserved signature byte range.</summary>
/// <param name="File">The owned file buffer.</param>
/// <param name="UpdateStart">The first byte of the incremental update.</param>
/// <param name="Capacity">The reserved signature bytes.</param>
/// <param name="ContentsStart">The opening angle bracket of the reserved contents.</param>
/// <param name="ContentsEnd">The first byte after the closing angle bracket.</param>
[DebuggerDisplay("PdfSigningFile: {File.Length} bytes, {Capacity} reserved")]
internal sealed record PdfSigningFile(byte[] File, int UpdateStart, int Capacity, int ContentsStart, int ContentsEnd);
