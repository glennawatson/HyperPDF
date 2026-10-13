// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A run of pages that becomes one document when a document is split.</summary>
/// <param name="Title">The outline title the part starts at, or <see langword="null"/> when the part has none.</param>
/// <param name="FirstPage">The zero based index of the part's first page.</param>
/// <param name="PageCount">The number of pages in the part.</param>
[DebuggerDisplay("PdfSplitPart: {Title} pages {FirstPage} to {FirstPage + PageCount - 1}")]
public sealed record PdfSplitPart(string? Title, int FirstPage, int PageCount);
