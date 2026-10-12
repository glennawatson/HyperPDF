// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;
using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Controls;

/// <summary>
/// Keys for moving around and grabbing text while the pages have the keyboard: Home and End go to the first and
/// last page, Ctrl+A selects all the text on the current page, Space held turns a drag into moving the pages, and
/// the auto-scroll and area tool keys.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>Handles the moving and grabbing keys before the other page keys.</summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandleNavigationKey(KeyEventArgs e)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        if (HandleAutoScrollKey(tab, e))
        {
            return true;
        }

        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
        {
            SelectAllOnPage(tab);
            return true;
        }

        return (e.KeyModifiers == KeyModifiers.None || e.Key == Key.Space) && HandlePlainNavigationKey(tab, e);
    }

    /// <summary>Handles Home, End, Escape and Space, pressed without Ctrl or Alt.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandlePlainNavigationKey(DocumentTabViewModel tab, KeyEventArgs e) =>
        e.Key switch
        {
            // In caret mode Home and End stay with the text cursor.
            Key.Home when !tab.IsCaretMode && tab.PageCount > 0 => GoToEdgePage(tab, 0),
            Key.End when !tab.IsCaretMode && tab.PageCount > 0 => GoToEdgePage(tab, tab.PageCount - 1),
            Key.Escape => EscapePageTool(tab),
            Key.Space => !tab.IsCaretMode && PressSpace(e),
            _ => false,
        };

    /// <summary>Goes to the first or last page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool GoToEdgePage(DocumentTabViewModel tab, int page)
    {
        tab.GoToPage(page);
        InvalidateVisual();
        return true;
    }

    /// <summary>Selects all the text on the current page when the Select All menu item asks.</summary>
    private void OnSelectAllRequested()
    {
        if (Tab is { } tab)
        {
            SelectAllOnPage(tab);
        }
    }

    /// <summary>Drops a drag in progress and shows the new tool's pointer when the tool changes.</summary>
    private void OnPageToolChanged()
    {
        _areaDragging = false;
        Cursor = ToolCursor();
        InvalidateVisual();
    }

    /// <summary>Selects all the text on the current page; the caret moves to its end, so Shift and the arrows adjust it.</summary>
    /// <param name="tab">The tab.</param>
    private void SelectAllOnPage(DocumentTabViewModel tab)
    {
        if (tab.TryGetDocument() is not { PageCount: > 0 } document)
        {
            return;
        }

        var page = Math.Clamp(tab.CurrentPageIndex, 0, document.PageCount - 1);
        var count = document.GetCharacterCount(page);
        ClearSelection();
        if (count <= 0)
        {
            return;
        }

        _selectionAnchor = (page, 0);
        _selectionFocus = (page, count - 1);
        _caret = (page, count - 1);
    }
}
