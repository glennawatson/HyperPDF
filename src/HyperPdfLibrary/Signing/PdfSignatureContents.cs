// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signing;

/// <summary>Identifies zero-filled bytes leased for the lifetime of a private signing workspace.</summary>
/// <param name="Buffer">The exclusively owned backing array.</param>
/// <param name="Length">The bytes reserved in that array.</param>
internal readonly record struct PdfSignatureContents(byte[] Buffer, int Length);
