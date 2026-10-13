// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>One document made by splitting a document.</summary>
/// <param name="Part">The pages the document holds.</param>
/// <param name="Bytes">The PDF bytes.</param>
[DebuggerDisplay("PdfSplitOutput: {Part}, {Bytes.Length} bytes")]
public sealed record PdfSplitOutput(PdfSplitPart Part, byte[] Bytes);
