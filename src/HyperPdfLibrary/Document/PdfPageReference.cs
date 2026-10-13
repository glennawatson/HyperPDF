// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A page of one of several documents.</summary>
/// <param name="Source">The zero based position of the document in the list of documents.</param>
/// <param name="Page">The zero based index of the page in that document.</param>
[DebuggerDisplay("PdfPageReference: document {Source} page {Page}")]
public readonly record struct PdfPageReference(int Source, int Page);
