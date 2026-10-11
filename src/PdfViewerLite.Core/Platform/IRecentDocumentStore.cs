// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Platform;

/// <summary>The desktop's list of recently used documents, shared with the file manager.</summary>
public interface IRecentDocumentStore
{
    /// <summary>Gets recently used PDF documents, newest first.</summary>
    /// <param name="maxCount">The maximum number to return.</param>
    /// <returns>The documents.</returns>
    IReadOnlyList<RecentDocument> GetRecent(int maxCount);

    /// <summary>Records that a document was opened.</summary>
    /// <param name="filePath">The file path.</param>
    void Add(string filePath);

    /// <summary>Removes a document from the recent list without deleting the file.</summary>
    /// <param name="filePath">The file path.</param>
    void Remove(string filePath);

    /// <summary>Clears the PDF documents shown in the recent list without deleting files.</summary>
    void Clear();
}
