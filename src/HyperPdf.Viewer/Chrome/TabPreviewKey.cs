// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the controls of a tab's preview were made from, so they are made again only when one of these changes.</summary>
/// <param name="Tab">The tab previewed.</param>
/// <param name="Item">The control of the tab, which the preview opens under.</param>
/// <param name="Page">The page the tab is on.</param>
/// <param name="Pages">How many pages the tab has.</param>
/// <param name="WindowWidth">The window width in device pixels.</param>
/// <param name="Scale">The display scale.</param>
internal readonly record struct TabPreviewKey(DocumentTabViewModel Tab, ChromeItem Item, int Page, int Pages, int WindowWidth, double Scale)
{
    /// <summary>Determines whether another key is made of the same things, comparing the tab and the control by identity.</summary>
    /// <param name="other">The other key, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when nothing has changed.</returns>
    internal bool IsSameAs(TabPreviewKey? other) =>
        other is { } key
        && ReferenceEquals(Tab, key.Tab)
        && ReferenceEquals(Item, key.Item)
        && Page == key.Page
        && Pages == key.Pages
        && WindowWidth == key.WindowWidth
        && Math.Abs(Scale - key.Scale) < double.Epsilon;
}
