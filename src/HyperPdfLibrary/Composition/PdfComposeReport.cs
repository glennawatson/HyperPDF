// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>What composing a document did.</summary>
/// <param name="PageCount">The number of pages added to the document.</param>
/// <param name="Notes">Things that could not be done as asked, in plain words; empty when everything was.</param>
[DebuggerDisplay("PdfComposeReport: {PageCount} pages, {Notes.Count} notes")]
public sealed record PdfComposeReport(int PageCount, IReadOnlyList<string> Notes);
