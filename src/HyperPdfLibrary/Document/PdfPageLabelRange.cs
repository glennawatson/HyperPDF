// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A run of pages that are numbered the same way, from its first page until the next range starts.</summary>
/// <param name="StartPage">The zero based page the range starts on.</param>
/// <param name="Style">How the number is written.</param>
/// <param name="Prefix">Text written before the number, such as <c>A-</c>; empty for none.</param>
/// <param name="FirstNumber">The number of the first page of the range, counted from 1.</param>
[DebuggerDisplay("PdfPageLabelRange: page {StartPage + 1}, {Style}, '{Prefix}', from {FirstNumber}")]
public sealed record PdfPageLabelRange(int StartPage, PdfPageLabelStyle Style, string Prefix, int FirstNumber);
