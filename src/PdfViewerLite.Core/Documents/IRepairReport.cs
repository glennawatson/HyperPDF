// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>Says whether a document was damaged and had to be repaired to be shown, and what was repaired.</summary>
public interface IRepairReport : IDocumentFeature
{
    /// <summary>Gets a value indicating whether reading the document repaired damage so far. Some damage shows only when a page is drawn, so ask again later.</summary>
    bool WasRepaired { get; }

    /// <summary>Gets the repairs made so far, in plain words, each once.</summary>
    /// <returns>The repairs; empty when the document was not repaired.</returns>
    IReadOnlyList<RepairNote> GetRepairs();
}
