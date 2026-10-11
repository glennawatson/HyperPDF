// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.Services;

/// <summary>A recent document store for platforms without a shared list.</summary>
internal sealed class NullRecentDocumentStore : IRecentDocumentStore
{
    /// <inheritdoc/>
    public IReadOnlyList<RecentDocument> GetRecent(int maxCount) => [];

    /// <inheritdoc/>
    public void Add(string filePath)
    {
    }

    /// <inheritdoc/>
    public void Remove(string filePath)
    {
    }

    /// <inheritdoc/>
    public void Clear()
    {
    }
}
