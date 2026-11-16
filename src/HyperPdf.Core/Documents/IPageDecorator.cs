// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

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
    /// <returns>A cold signal giving one value after the pages have changed; unsubscribing cancels before the edit.</returns>
    IObservable<RxVoid> StampText(ReadOnlyMemory<int> pages, PageTextStamp stamp);

    /// <summary>Fills the background of pages with a colour, behind what the page draws.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="argb">The colour as an opaque ARGB value.</param>
    /// <returns>A cold signal giving one value after the pages have changed; unsubscribing cancels before the edit.</returns>
    IObservable<RxVoid> FillBackground(ReadOnlyMemory<int> pages, uint argb);

    /// <summary>Crops pages by taking an amount off each side, or shows the whole page again when every margin is zero.</summary>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="margins">What to take off each side, as the page is shown.</param>
    /// <returns>A cold signal giving one value after the pages have changed; unsubscribing cancels before the edit.</returns>
    IObservable<RxVoid> Crop(ReadOnlyMemory<int> pages, PageMargins margins);
}
