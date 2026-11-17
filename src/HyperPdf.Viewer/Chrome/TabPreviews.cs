// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the preview that opens under a tab the pointer rests on: the page the tab shows as a picture, the name and where in the document it is.</summary>
internal static class TabPreviews
{
    /// <summary>The number of the picture control, which no row of the pages panel or dialog has.</summary>
    internal const int PictureIndex = -2;

    /// <summary>The text rows under the picture: the name, and the page's place.</summary>
    private const int TextRows = 2;

    /// <summary>The padding on each side of the picture and the text, which is one on the left and one on the right.</summary>
    private const int Sides = 2;

    /// <summary>Adds the preview of a tab.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="hovered">The control of the tab, which the preview opens under.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="scale">The display scale.</param>
    /// <param name="windowWidth">The window width in device pixels.</param>
    internal static void Add(List<ChromeItem> items, DocumentTabViewModel tab, ChromeItem hovered, TextRuns runs, in ChromeMetrics metrics, double scale, int windowWidth)
    {
        var pictureWidth = (int)Math.Round(DocumentTabViewModel.PreviewWidth * scale);
        var pictureHeight = PictureHeight(tab, pictureWidth);
        var padding = metrics.Padding;
        var width = Math.Min(pictureWidth + (padding * Sides), windowWidth);
        var row = metrics.TabHeight;
        var height = padding + (pictureHeight > 0 ? pictureHeight + padding : 0) + (row * TextRows) + padding;
        var x = (int)Math.Clamp(hovered.Bounds.X, 0, Math.Max(0, windowWidth - width));
        var top = metrics.TabHeight;
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(x, top, width, height), true, false));

        var y = top + padding;
        if (pictureHeight > 0)
        {
            items.Add(new(ChromeItemKind.Thumbnail, ChromeCommand.None, PictureIndex, string.Empty, new(x + padding, y, width - (padding * Sides), pictureHeight), true, false));
            y += pictureHeight + padding;
        }

        var room = width - (padding * Sides);
        var style = ChromeBars.Style(metrics);
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, ChromeText.Fit(tab.FileName, style, room, runs), new(x + padding, y, room, row), true, false));
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, ChromeText.Fit(PageText(tab), style, room, runs), new(x + padding, y + row, room, row), true, false));
    }

    /// <summary>Gets the page of a tab the preview shows.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The zero based page, which is the one the tab is on.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int PageOf(DocumentTabViewModel tab) => Math.Max(0, tab.CurrentPageIndex);

    /// <summary>Gets the height of the picture, which keeps the shape of the page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="width">The width of the picture.</param>
    /// <returns>The height, or zero when the tab has not said how large its pages are.</returns>
    private static int PictureHeight(DocumentTabViewModel tab, int width)
    {
        var page = PageOf(tab);
        if ((uint)page >= (uint)tab.Source.PageCount)
        {
            return 0;
        }

        var size = tab.Source.PageSizes[page];
        return size.Width > 0 ? (int)Math.Round(width * (size.Height / size.Width)) : 0;
    }

    /// <summary>Says where in the document the page is.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The text, such as "Page 3 of 12".</returns>
    private static string PageText(DocumentTabViewModel tab) =>
        tab.PageCount > 0 ? UiText.Format("Page {0} of {1}", PageOf(tab) + 1, tab.PageCount) : UiText.Get("No pages");
}
