// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Controls;

/// <summary>
/// The current page when two pages share a row: the position and page indicator stay on the page asked for, or the
/// page they were on, while that page's spread is on screen, so switching arrangement keeps the same page.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The page a navigation asked for, until the next position report; -1 when none.</summary>
    private int _requestedPage = -1;

    /// <summary>Gets the pages to report: the requested or current page wherever its row is the row found.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="top">The vertical offset of the top of the content in view.</param>
    /// <param name="middle">The vertical offset of the middle of the viewport.</param>
    /// <param name="topPage">The page at the top of the viewport.</param>
    /// <param name="middlePage">The page in the middle of the viewport, shown as the current page.</param>
    private void GetCurrentPages(DocumentTabViewModel tab, double top, double middle, out int topPage, out int middlePage)
    {
        var preferred = _requestedPage >= 0 ? _requestedPage : tab.CurrentPageIndex;
        _requestedPage = -1;
        topPage = _layout.GetCurrentPage(top, preferred);
        middlePage = _layout.GetCurrentPage(middle, preferred);
    }
}
