// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Reads and sets how the pages of a document are numbered for a reader, such as <c>i, ii, iii</c> and then <c>A-1, A-2</c>.</summary>
public interface IPageLabeler : IDocumentFeature
{
    /// <summary>Gets the ranges of page labels.</summary>
    /// <returns>The ranges in page order; empty when pages are numbered 1, 2, 3.</returns>
    PageLabelRun[] GetRanges();

    /// <summary>Replaces the ranges of page labels as one undo step.</summary>
    /// <param name="ranges">The ranges in page order; the first starts on the first page.</param>
    /// <exception cref="ArgumentException">There is no range, the first does not start on the first page, or the starts do not rise.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range starts on a page that does not exist or its first number is below 1.</exception>
    void SetRanges(PageLabelRun[] ranges);
}
