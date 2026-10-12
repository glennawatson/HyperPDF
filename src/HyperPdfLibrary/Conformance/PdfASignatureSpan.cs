// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Where a signature's /Contents string lies in the file.</summary>
/// <param name="Start">The offset of its opening bracket.</param>
/// <param name="End">The offset after its closing bracket.</param>
internal readonly record struct PdfASignatureSpan(long Start, long End);
