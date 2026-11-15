// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>
/// Adds to pages what Adobe's Edit PDF tools add around the page's own content: watermarks, headers and footers, page numbers, Bates
/// numbers, a background colour, and a crop. Each call is one undo step and changes the open document in place.
/// </summary>
public interface IPageDecorator : IDocumentFeature
{
    /// <summary>Stamps text on pages: a watermark, a header or footer, page numbers or Bates numbers.</summary>
    /// <param name="pages">The zero based pages, in the order they are numbered from <see cref="PageTextStamp.FirstNumber"/>.</param>
    /// <param name="stamp">The words and how they are drawn.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    ValueTask StampTextAsync(ReadOnlyMemory<int> pages, PageTextStamp stamp, CancellationToken cancellationToken);

    /// <summary>Fills the background of pages with a colour, behind what the page draws.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="argb">The colour as an opaque ARGB value.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    ValueTask FillBackgroundAsync(ReadOnlyMemory<int> pages, uint argb, CancellationToken cancellationToken);

    /// <summary>Crops pages by taking an amount off each side, or shows the whole page again when every margin is zero.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="margins">What to take off each side, as the page is shown.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the pages have changed.</returns>
    ValueTask CropAsync(ReadOnlyMemory<int> pages, PageMargins margins, CancellationToken cancellationToken);
}
