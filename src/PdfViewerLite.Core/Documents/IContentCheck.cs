// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>Finds content a document holds that cannot be shown or run.</summary>
public interface IContentCheck : IDocumentFeature
{
    /// <summary>Checks the document as a whole: its form type and document scripts.</summary>
    /// <returns>What cannot be shown.</returns>
    UnsupportedContent CheckDocument();

    /// <summary>Checks one page's annotations and field scripts.</summary>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>What on the page cannot be shown or run.</returns>
    UnsupportedContent CheckPage(int pageIndex);
}
