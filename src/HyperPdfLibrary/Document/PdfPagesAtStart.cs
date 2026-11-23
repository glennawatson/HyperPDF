// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>The pages as an edit transaction started, so its commit can keep them for undo.</summary>
/// <param name="Pages">The pages read before the transaction, or <see langword="null"/> when none were read.</param>
internal sealed record PdfPagesAtStart(PdfPageSet? Pages);
