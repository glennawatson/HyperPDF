// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Documents;

/// <summary>
/// What the viewer does about a document's open hints, after the user's own settings have had their say. A hint the user has
/// turned off, or that the user's own choice overrides, is left out.
/// </summary>
/// <param name="StartPage">The zero-based page to open at, or -1 to leave the page as it is.</param>
/// <param name="Panel">The side panel to show, or <see cref="OpenPanel.None"/> to leave the sidebar as it is.</param>
/// <param name="Layout">The page arrangement to use, or <see langword="null"/> to keep the user's choice.</param>
/// <param name="RightToLeft">Whether spreads read right to left.</param>
/// <param name="UseDocumentTitle">Whether the document's own title is the window title.</param>
/// <param name="Print">The print defaults to start the print window with, or <see langword="null"/> for the viewer's own.</param>
[DebuggerDisplay("OpenHintPlan: page {StartPage} {Panel} {Layout}")]
public sealed record OpenHintPlan(
    int StartPage,
    OpenPanel Panel,
    PageLayoutMode? Layout,
    bool RightToLeft,
    bool UseDocumentTitle,
    PrintDefaults? Print)
{
    /// <summary>Works out what to do about a document's hints.</summary>
    /// <param name="hints">What the document asks for.</param>
    /// <param name="settings">The user's settings, which decide which hints are followed.</param>
    /// <param name="hasChosenPage">Whether the reader's own position (a page restored from the last visit, or a link followed) already decides the page.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hints"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    public static OpenHintPlan Create(DocumentOpenHints hints, AppSettings settings, bool hasChosenPage)
    {
        ArgumentNullException.ThrowIfNull(hints);
        ArgumentNullException.ThrowIfNull(settings);
        var follow = settings.FollowOpenAction && !hasChosenPage && hints.OpenPage >= 0;
        var layout = settings.FollowPageLayout && hints.Layout != PageLayoutMode.Single ? hints.Layout : (PageLayoutMode?)null;
        return new(
            follow ? hints.OpenPage : -1,
            settings.FollowPageMode ? hints.Panel : OpenPanel.None,
            layout,
            settings.FollowPageLayout && hints.RightToLeft,
            settings.FollowDisplayDocTitle && hints.DisplayDocTitle,
            settings.FollowPrintDefaults ? hints.Print : null);
    }
}
