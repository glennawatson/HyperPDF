// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.ViewModels;

/// <summary>Brings the sidebar list of a tab up to date when its panel is shown.</summary>
internal static class TabSidebarPane
{
    /// <summary>Brings the annotation or layer list up to date when its panel is shown; both skip work while hidden.</summary>
    /// <param name="tab">The tab.</param>
    internal static void Show(DocumentTabViewModel tab)
    {
        if (tab.IsAnnotationsMode)
        {
            _ = tab.Annotations.ShowItems();
            return;
        }

        if (tab.IsLayersMode)
        {
            tab.Layers.ShowForPage();
        }
    }
}
