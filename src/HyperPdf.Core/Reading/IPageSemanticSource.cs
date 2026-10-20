// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Reading;

/// <summary>Gives what assistive technology needs from a page beyond its reading blocks.</summary>
public interface IPageSemanticSource : IDocumentFeature
{
    /// <summary>Gets a page's semantic items.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The items: empty cells and artifacts in the order of the page, then form fields and annotations; empty for a page that is out of range.</returns>
    IReadOnlyList<PageSemanticItem> GetItems(int pageIndex);
}
