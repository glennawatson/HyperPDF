// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Settings;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Applies the hints a document makes about how to open, as far as the reader's settings allow.</summary>
internal static class TabOpenHints
{
    /// <summary>
    /// Applies a document's open hints to its tab. A page the reader chose, by returning to where they stopped or following
    /// a link, always wins over the document's own opening page.
    /// </summary>
    /// <param name="tab">The tab, which has just loaded its document.</param>
    /// <param name="settings">The reader's settings.</param>
    internal static void Apply(DocumentTabViewModel tab, AppSettings settings)
    {
        var hints = tab.Source.Metadata?.OpenHints ?? DocumentOpenHints.None;
        var chosen = tab.CurrentPageIndex > 0 || (settings.ReopenAtLastPage && LastPages.IsRemembered(settings.LastPages, tab.FilePath));
        var plan = OpenHintPlan.Create(hints, settings, chosen);
        if (plan.StartPage >= 0)
        {
            tab.GoToPage(plan.StartPage);
        }

        if (plan.Layout is { } layout)
        {
            tab.LayoutMode = layout;
        }

        tab.RightToLeft = plan.RightToLeft;
        tab.PrintDefaults = plan.Print;

        // A portfolio is its files, so unless the document asks for another panel the reader sees them first.
        ShowPanel(tab, plan.Panel == OpenPanel.None && tab.Attachments.ShowsPortfolio ? OpenPanel.Attachments : plan.Panel);
    }

    /// <summary>Gets the title a tab shows: the document's own when it asks for that and the reader allows, otherwise the file name.</summary>
    /// <param name="metadata">The document's metadata, or <see langword="null"/> before it is read.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="settings">The reader's settings.</param>
    /// <returns>The title.</returns>
    internal static string TitleOf(DocumentMetadata? metadata, string fileName, AppSettings settings)
    {
        var showsTitle = settings.FollowDisplayDocTitle && metadata?.OpenHints.DisplayDocTitle == true;
        return showsTitle && !string.IsNullOrWhiteSpace(metadata?.Title) ? metadata.Title : fileName;
    }

    /// <summary>Shows the sidebar panel a document asks for.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="panel">The panel; none leaves the sidebar as it is.</param>
    private static void ShowPanel(DocumentTabViewModel tab, OpenPanel panel)
    {
        SidebarMode? mode = panel switch
        {
            OpenPanel.Outline => SidebarMode.Outline,
            OpenPanel.Thumbnails => SidebarMode.Thumbnails,
            OpenPanel.Attachments => SidebarMode.Attachments,
            OpenPanel.Layers => SidebarMode.Layers,
            _ => null,
        };
        if (mode is not { } shown)
        {
            return;
        }

        tab.SidebarMode = shown;
        tab.SidebarVisible = true;
    }
}
