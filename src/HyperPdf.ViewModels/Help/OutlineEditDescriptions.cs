// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Help;

/// <summary>Explains the bookmark editing controls in tooltips and to screen readers.</summary>
public static class OutlineEditDescriptions
{
    /// <summary>Gets the explanation for the add button.</summary>
    public static string Add => "Add a bookmark that goes to the page you are on. It goes below the chosen bookmark, or at the top when none is chosen.";

    /// <summary>Gets the explanation for the rename button.</summary>
    public static string Rename => "Change the title of the chosen bookmark.";

    /// <summary>Gets the explanation for the remove button.</summary>
    public static string Remove => "Remove the chosen bookmark and the bookmarks below it. Undo puts them back.";

    /// <summary>Gets the explanation for moving a bookmark up.</summary>
    public static string MoveUp => "Move the chosen bookmark above the one before it.";

    /// <summary>Gets the explanation for moving a bookmark down.</summary>
    public static string MoveDown => "Move the chosen bookmark below the one after it.";

    /// <summary>Gets the explanation for nesting a bookmark.</summary>
    public static string Indent => "Make the chosen bookmark a child of the bookmark above it.";

    /// <summary>Gets the explanation for un-nesting a bookmark.</summary>
    public static string Outdent => "Move the chosen bookmark out of its parent, to just after the parent.";

    /// <summary>Gets the explanation for the undo button.</summary>
    public static string Undo => "Undo the latest edit of the document, such as a bookmark change.";

    /// <summary>Gets the explanation of the keys that work in the bookmark list.</summary>
    public static string Keys => "In the bookmark list: F2 renames, Delete removes, Ctrl+Shift+Up and Ctrl+Shift+Down move, Ctrl+Shift+Right nests and Ctrl+Shift+Left un-nests the chosen bookmark.";
}
