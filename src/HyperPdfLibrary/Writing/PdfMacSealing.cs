// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Writing;

/// <summary>What a trailer writer needs to give its revision a PDF MAC.</summary>
/// <param name="Seal">The seal, or <see langword="null"/> when the revision needs no PDF MAC.</param>
/// <param name="Prefix">The bytes of the file before the writer's first byte, or <see langword="null"/> when the writer starts the file.</param>
/// <param name="CancellationToken">Cancels the hashing.</param>
[DebuggerDisplay("PdfMacSealing: {Seal}")]
internal readonly record struct PdfMacSealing(PdfMacSeal? Seal, PdfByteSource? Prefix, CancellationToken CancellationToken);
