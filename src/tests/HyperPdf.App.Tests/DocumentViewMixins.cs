// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using HyperPdf.App.Views;

namespace HyperPdf.App.Tests;

/// <summary>Finds the named controls inside the views a document view is made of, so tests read as they did when it was one view.</summary>
internal static class DocumentViewMixins
{
    /// <summary>Adds the search for a named control in a view and the views it is made of.</summary>
    /// <param name="root">The view to search.</param>
    extension(Control root)
    {
        /// <summary>
        /// Finds a named control in the view or in any view inside it, including the items of menus and flyouts, which are not
        /// in the visual tree but are in the name scope of the view that declares them.
        /// </summary>
        /// <typeparam name="TControl">The kind of control wanted.</typeparam>
        /// <param name="name">The control's name.</param>
        /// <returns>The control.</returns>
        /// <exception cref="InvalidOperationException">No view holds a control with that name.</exception>
        internal TControl FindNamed<TControl>(string name)
            where TControl : Control
        {
            if (root.FindControl<TControl>(name) is { } own)
            {
                return own;
            }

            foreach (var part in root.GetVisualDescendants().OfType<UserControl>())
            {
                if (part.FindControl<TControl>(name) is { } found)
                {
                    return found;
                }
            }

            throw new InvalidOperationException($"{name} is not in the view.");
        }
    }

    /// <summary>Adds the controls of the tool bars, the sidebar and the page editors.</summary>
    /// <param name="view">The document view.</param>
    extension(DocumentView view)
    {
        /// <summary>Gets the Undo button of the Annotate tools.</summary>
        internal Button UndoButton => FindIn<AnnotateBarView, Button>(view, "UndoButton");

        /// <summary>Gets the Redo button of the Annotate tools.</summary>
        internal Button RedoButton => FindIn<AnnotateBarView, Button>(view, "RedoButton");

        /// <summary>Gets the Stamp button.</summary>
        internal Button StampButton => FindIn<AnnotateBarView, Button>(view, "StampButton");

        /// <summary>Gets the Draft stamp item.</summary>
        internal MenuItem DraftItem => FindIn<AnnotateBarView, MenuItem>(view, "DraftItem");

        /// <summary>Gets the Rectangle item.</summary>
        internal MenuItem RectangleItem => FindIn<AnnotateBarView, MenuItem>(view, "RectangleItem");

        /// <summary>Gets the Red colour item.</summary>
        internal MenuItem RedItem => FindIn<AnnotationStyleView, MenuItem>(view, "RedItem");

        /// <summary>Gets the comment list.</summary>
        internal ListBox AnnotationList => FindIn<AnnotationListView, ListBox>(view, "AnnotationList");

        /// <summary>Gets the comment filter box.</summary>
        internal TextBox AnnotationFilterBox => FindIn<AnnotationListView, TextBox>(view, "AnnotationFilterBox");

        /// <summary>Gets the panel of comment filters.</summary>
        internal StackPanel AnnotationFiltersPanel => FindIn<AnnotationListView, StackPanel>(view, "AnnotationFiltersPanel");

        /// <summary>Gets the toggle that shows the comment filters.</summary>
        internal ToggleButton AnnotationFiltersToggle => FindIn<AnnotationListView, ToggleButton>(view, "AnnotationFiltersToggle");

        /// <summary>Gets the button that clears the comment filter.</summary>
        internal Button ClearAnnotationFilterButton => FindIn<AnnotationListView, Button>(view, "ClearAnnotationFilterButton");

        /// <summary>Gets the text shown when no comment matches.</summary>
        internal TextBlock NoMatchesText => FindIn<AnnotationListView, TextBlock>(view, "NoMatchesText");

        /// <summary>Gets the auto-scroll bar.</summary>
        internal Border AutoScrollBar => FindIn<AutoScrollBarView, Border>(view, "AutoScrollBar");

        /// <summary>Gets the Fill and Sign bar.</summary>
        internal Border FillSignBar => FindIn<FillSignBarView, Border>(view, "FillSignBar");

        /// <summary>Gets the Fill and Sign toggle.</summary>
        internal ToggleButton FillSignToggle => FindIn<ActionBarView, ToggleButton>(view, "FillSignToggle");

        /// <summary>Gets the Snapshot tool item.</summary>
        internal MenuItem SnapshotToolItem => FindIn<ActionBarView, MenuItem>(view, "SnapshotToolItem");

        /// <summary>Gets the Zoom 100% item.</summary>
        internal MenuItem Zoom100Item => FindIn<NavigationBarView, MenuItem>(view, "Zoom100Item");

        /// <summary>Gets the Single Page layout item.</summary>
        internal MenuItem SingleLayoutItem => FindIn<ViewMenuView, MenuItem>(view, "SingleLayoutItem");

        /// <summary>Gets the Two Pages layout item.</summary>
        internal MenuItem DualLayoutItem => FindIn<ViewMenuView, MenuItem>(view, "DualLayoutItem");

        /// <summary>Gets the Two Pages with Cover layout item.</summary>
        internal MenuItem CoverLayoutItem => FindIn<ViewMenuView, MenuItem>(view, "CoverLayoutItem");

        /// <summary>Gets the Scroll Down item.</summary>
        internal MenuItem VerticalScrollItem => FindIn<ViewMenuView, MenuItem>(view, "VerticalScrollItem");

        /// <summary>Gets the Scroll Sideways item.</summary>
        internal MenuItem HorizontalScrollItem => FindIn<ViewMenuView, MenuItem>(view, "HorizontalScrollItem");

        /// <summary>Gets the Wrap Pages item.</summary>
        internal MenuItem WrappedScrollItem => FindIn<ViewMenuView, MenuItem>(view, "WrappedScrollItem");

        /// <summary>Gets the Page by Page item.</summary>
        internal MenuItem PageByPageItem => FindIn<ViewMenuView, MenuItem>(view, "PageByPageItem");

        /// <summary>Gets the First Page item.</summary>
        internal MenuItem FirstPageItem => FindIn<ViewMenuView, MenuItem>(view, "FirstPageItem");

        /// <summary>Gets the Hand tool item.</summary>
        internal MenuItem HandToolItem => FindIn<ViewMenuView, MenuItem>(view, "HandToolItem");

        /// <summary>Gets the Select Text tool item.</summary>
        internal MenuItem SelectTextToolItem => FindIn<ViewMenuView, MenuItem>(view, "SelectTextToolItem");

        /// <summary>Gets the Zoom to Area tool item.</summary>
        internal MenuItem ZoomAreaToolItem => FindIn<ViewMenuView, MenuItem>(view, "ZoomAreaToolItem");

        /// <summary>Gets the colour separation bar.</summary>
        internal Border SeparationBar => FindIn<DocumentBarsView, Border>(view, "SeparationBar");

        /// <summary>Gets the text on the colour separation bar.</summary>
        internal TextBlock SeparationText => FindIn<DocumentBarsView, TextBlock>(view, nameof(SeparationText));

        /// <summary>Gets the label of the separation choice.</summary>
        internal TextBlock SeparationChoiceLabel => FindIn<DocumentBarsView, TextBlock>(view, "SeparationChoiceLabel");

        /// <summary>Gets the box that chooses the separation shown.</summary>
        internal ComboBox SeparationBox => FindIn<DocumentBarsView, ComboBox>(view, "SeparationBox");

        /// <summary>Gets the text format bar.</summary>
        internal Border TextFormatBar => FindIn<TextFormatBarView, Border>(view, "TextFormatBar");

        /// <summary>Gets the font family box.</summary>
        internal ComboBox FontFamilyBox => FindIn<TextFormatBarView, ComboBox>(view, "FontFamilyBox");

        /// <summary>Gets the font size box.</summary>
        internal ComboBox FontSizeBox => FindIn<TextFormatBarView, ComboBox>(view, "FontSizeBox");

        /// <summary>Gets the Bold toggle.</summary>
        internal ToggleButton BoldToggle => FindIn<TextFormatBarView, ToggleButton>(view, "BoldToggle");

        /// <summary>Gets the Italic toggle.</summary>
        internal ToggleButton ItalicToggle => FindIn<TextFormatBarView, ToggleButton>(view, "ItalicToggle");

        /// <summary>Gets the Text Properties button.</summary>
        internal Button TextPropertiesButton => FindIn<TextFormatBarView, Button>(view, "TextPropertiesButton");

        /// <summary>Gets the editor placed over the form field being filled in.</summary>
        internal TextBox FieldEditor => FindIn<FieldEditorView, TextBox>(view, "FieldEditor");

        /// <summary>Gets the editor placed on the page where text is typed.</summary>
        internal TextBox PageTextEditor => FindIn<PageTextEditorView, TextBox>(view, "PageTextEditor");
    }

    /// <summary>Finds a named control inside the first view of a kind that a document view holds.</summary>
    /// <typeparam name="TPart">The kind of view that holds the control.</typeparam>
    /// <typeparam name="TControl">The kind of control wanted.</typeparam>
    /// <param name="view">The document view.</param>
    /// <param name="name">The control's name.</param>
    /// <returns>The control.</returns>
    private static TControl FindIn<TPart, TControl>(DocumentView view, string name)
        where TPart : Control
        where TControl : Control =>
        view.GetVisualDescendants().OfType<TPart>().First().FindNamed<TControl>(name);
}
