// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Help;

/// <summary>Explains the thumbnail page actions in tooltips and to screen readers.</summary>
public static class PageActionDescriptions
{
    /// <summary>Gets the page action menu's explanation.</summary>
    public static string Actions => UiText.Get("Edit or save copies of the pages selected in the thumbnail list.");

    /// <summary>Gets the deletion explanation.</summary>
    public static string Delete => UiText.Get("Remove the selected pages. Undo can restore them. Shortcut: Delete.");

    /// <summary>Gets the clockwise rotation explanation.</summary>
    public static string Clockwise => UiText.Get("Turn the selected pages clockwise by a quarter turn.");

    /// <summary>Gets the anticlockwise rotation explanation.</summary>
    public static string Counterclockwise => UiText.Get("Turn the selected pages anticlockwise by a quarter turn.");

    /// <summary>Gets the duplication explanation.</summary>
    public static string Duplicate => UiText.Get("Add copies of the selected pages after the selection.");

    /// <summary>Gets the explanation for moving toward the start.</summary>
    public static string Earlier => UiText.Get("Move the selected pages one place toward the start. Shortcut: Ctrl+Alt+Up.");

    /// <summary>Gets the explanation for moving toward the end.</summary>
    public static string Later => UiText.Get("Move the selected pages one place toward the end. Shortcut: Ctrl+Alt+Down.");

    /// <summary>Gets the insertion explanation.</summary>
    public static string Insert => UiText.Get("Choose PDFs to insert before the first selected page.");

    /// <summary>Gets the merge explanation.</summary>
    public static string Merge => UiText.Get("Choose PDFs to append at the end of this document.");

    /// <summary>Gets the extraction explanation.</summary>
    public static string Extract => UiText.Get("Save the selected pages in a new PDF file.");

    /// <summary>Gets the undo explanation.</summary>
    public static string Undo => UiText.Get("Restore the document before its most recent edit.");

    /// <summary>Gets the redo explanation.</summary>
    public static string Redo => UiText.Get("Apply the most recently undone edit again.");
}
