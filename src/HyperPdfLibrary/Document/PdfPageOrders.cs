// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>The pages before and after a committed edit, kept so undo and redo need not read the page tree again.</summary>
/// <param name="Before">The pages before the edit; none of them made, and never handed out.</param>
/// <param name="After">The pages the edit left, or <see langword="null"/> when it left none, which makes a redo read the tree.</param>
[DebuggerDisplay("PdfPageOrders: {Before.Count} pages before")]
internal sealed record PdfPageOrders(PdfPageSet Before, PdfPageSet? After);
