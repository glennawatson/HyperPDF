// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The separation pages that together print one page of a preseparated document.</summary>
/// <param name="PrintedPage">The printed page's one-based number, counting the document's printed pages in order.</param>
/// <param name="Pages">The separation pages, in the order the file lists them.</param>
[DebuggerDisplay("SeparationGroup: printed page {PrintedPage}")]
public sealed record SeparationGroup(int PrintedPage, SeparationPage[] Pages);
