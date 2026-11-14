// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Reading;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Viewer.Accessibility;

/// <summary>Makes the elements of the viewer's accessibility tree: the application, its window, the document and the blocks of the page shown.</summary>
internal static class ViewerNodes
{
    /// <summary>The number of the application element, the root of the tree.</summary>
    internal const int ApplicationId = 1;

    /// <summary>The number of the window element.</summary>
    internal const int WindowId = 2;

    /// <summary>The number of the document element, which holds the page text and the keyboard focus.</summary>
    internal const int DocumentId = 3;

    /// <summary>The number of the first block element of the page shown. Blocks are numbered in reading order from here.</summary>
    internal const int FirstBlockId = 100;

    /// <summary>The number of the first control of the bars. Controls are numbered in the order they are laid out from here.</summary>
    internal const int FirstControlId = 1_000_000;

    /// <summary>The number of the first control of the open menu, dialog or search bar.</summary>
    internal const int FirstOverlayId = 2_000_000;

    /// <summary>The states of an element that is there to be read.</summary>
    private const AccessibleStates Readable = AccessibleStates.Enabled | AccessibleStates.Showing;

    /// <summary>The actions of a control that can be pressed.</summary>
    private static readonly string[] ClickActions = [AccessibleActions.Click];

    /// <summary>Makes the application element.</summary>
    /// <param name="children">The numbers of its windows.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateApplication(IReadOnlyList<int> children) =>
        new(ApplicationId, -1, AccessibleRole.Application, "Hyper PDF Viewer", string.Empty, Readable, default, children, 0, 0, 0, string.Empty, [], null, 0);

    /// <summary>Makes the window element.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="bounds">Where the window's drawing area is on the screen.</param>
    /// <param name="handle">The operating system's number for the window.</param>
    /// <param name="children">The numbers of the document and the controls, back to front.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateWindow(string title, in AccessibleBounds bounds, long handle, IReadOnlyList<int> children) =>
        new(WindowId, ApplicationId, AccessibleRole.Window, title, string.Empty, Readable | AccessibleStates.Active, bounds, children, 0, 0, 0, string.Empty, [], null, handle);

    /// <summary>Makes the document element, which a screen reader reads the page from.</summary>
    /// <param name="name">What the document is, such as the page and the page count.</param>
    /// <param name="help">The picked annotation, or empty.</param>
    /// <param name="bounds">Where the document is on the screen.</param>
    /// <param name="text">The text of the page shown, in reading order.</param>
    /// <param name="children">The numbers of the page's blocks.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateDocument(string name, string help, in AccessibleBounds bounds, string text, IReadOnlyList<int> children) => new(
        DocumentId,
        WindowId,
        AccessibleRole.Document,
        name,
        help,
        Readable | AccessibleStates.Focusable | AccessibleStates.Focused | AccessibleStates.ReadOnly | AccessibleStates.MultiLine,
        bounds,
        children,
        0,
        0,
        0,
        "main",
        [],
        AccessibleText.Plain(text),
        0);

    /// <summary>Makes the element of one block of the page, so a screen reader can move by heading, list item, table cell and figure.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="block">The block.</param>
    /// <param name="bounds">Where the block is on the screen.</param>
    /// <param name="place">The block's place in its list, or the default when it is not a list item.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateBlock(int id, ReadingBlock block, in AccessibleBounds bounds, in ListPlace place) => new(
        id,
        DocumentId,
        RoleOf(block.Kind),
        block.Text,
        Describe(block, place),
        Readable,
        bounds,
        [],
        block.Kind == ReadingBlockKind.Heading ? block.Level : 0,
        place.Position,
        place.Size,
        string.Empty,
        [],
        AccessibleText.Plain(block.Text),
        0);

    /// <summary>Makes the element of one of the window's controls: a tab, a button, a menu row, a text box or a label.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="item">The control.</param>
    /// <param name="bounds">Where the control is on the screen.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateControl(int id, ChromeItem item, in AccessibleBounds bounds)
    {
        var pressable = item.Command != ChromeCommand.None && item.Enabled;
        var states = AccessibleStates.Showing
            | (item.Enabled ? AccessibleStates.Enabled : AccessibleStates.None)
            | (pressable ? AccessibleStates.Focusable : AccessibleStates.None)
            | (item.Selected && item.Kind != ChromeItemKind.Field ? AccessibleStates.Selected : AccessibleStates.None)
            | (item.Kind == ChromeItemKind.Field ? AccessibleStates.Editable | AccessibleStates.Focusable | AccessibleStates.Focused : AccessibleStates.None);
        return new(
            id,
            WindowId,
            RoleOf(item.Kind),
            item.Text,
            string.Empty,
            states,
            bounds,
            [],
            0,
            0,
            0,
            string.Empty,
            pressable ? ClickActions : [],
            item.Kind == ChromeItemKind.Field ? AccessibleText.Plain(item.Text) : null,
            0);
    }

    /// <summary>Tells whether a control is published to assistive technology. Backgrounds and lines are not.</summary>
    /// <param name="kind">How the control is drawn.</param>
    /// <returns><see langword="true"/> for a control with text to read or something to press.</returns>
    internal static bool IsPublished(ChromeItemKind kind) => kind is not (ChromeItemKind.Panel or ChromeItemKind.Popup or ChromeItemKind.Separator);

    /// <summary>Finds the role of a control.</summary>
    /// <param name="kind">How the control is drawn.</param>
    /// <returns>The role.</returns>
    private static AccessibleRole RoleOf(ChromeItemKind kind) => kind switch
    {
        ChromeItemKind.Button => AccessibleRole.Button,
        ChromeItemKind.Tab => AccessibleRole.TabItem,
        ChromeItemKind.Row => AccessibleRole.MenuItem,
        ChromeItemKind.Field => AccessibleRole.Edit,
        ChromeItemKind.Heading => AccessibleRole.Heading,
        _ => AccessibleRole.Label,
    };

    /// <summary>Finds the role of a block kind.</summary>
    /// <param name="kind">The block kind.</param>
    /// <returns>The role.</returns>
    private static AccessibleRole RoleOf(ReadingBlockKind kind) => kind switch
    {
        ReadingBlockKind.Heading => AccessibleRole.Heading,
        ReadingBlockKind.ListItem => AccessibleRole.ListItem,
        ReadingBlockKind.TableCell => AccessibleRole.TableCell,
        ReadingBlockKind.Figure => AccessibleRole.Image,
        ReadingBlockKind.Caption => AccessibleRole.Label,
        _ => AccessibleRole.Paragraph,
    };

    /// <summary>Says what a block is when its role alone does not, and when its language changes.</summary>
    /// <param name="block">The block.</param>
    /// <param name="place">The block's place in its list.</param>
    /// <returns>The description, or empty.</returns>
    private static string Describe(ReadingBlock block, in ListPlace place)
    {
        var role = block.Kind switch
        {
            ReadingBlockKind.ListItem when place.Size > 0 => UiText.Format("List item {0} of {1}", place.Position, place.Size),
            ReadingBlockKind.TableCell when block.Row > 0 => UiText.Format("Table cell, row {0}, column {1}", block.Row, block.Column),
            ReadingBlockKind.TableCell => UiText.Get("Table cell"),
            ReadingBlockKind.Figure => UiText.Get("Figure"),
            ReadingBlockKind.Caption => UiText.Get("Caption"),
            ReadingBlockKind.Footnote => UiText.Get("Footnote"),
            _ => string.Empty,
        };

        // A change of language is said so a screen reader can switch voices or tell the reader.
        if (block.Language is not { Length: > 0 } language)
        {
            return role;
        }

        var spoken = UiText.Format("language {0}", language);
        return role.Length == 0 ? spoken : $"{role}, {spoken}";
    }
}
