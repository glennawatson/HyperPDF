// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive right-click context menu stream and menu builders for <see cref="PageCanvas"/>: selected text, picked
/// annotations, blank pages and choice form fields.
/// </summary>
internal static class PageCanvasContextMenuMixins
{
    /// <summary>The size of colour swatches in menus.</summary>
    private const double SwatchSize = 12;

    /// <summary>The corner radius of colour swatches.</summary>
    private const double SwatchRadius = 2;

    /// <summary>Adds the right-click context menu stream and menu builders to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the unhandled right-button pointer presses on the canvas, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The right-click menu requests.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ContextMenuClick> RightClicks(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryMatchRightClick);

        /// <summary>Opens the right-click menu for whatever is under the pointer.</summary>
        /// <param name="tab">The active tab.</param>
        /// <param name="position">The canvas point.</param>
        internal void ShowContextMenu(DocumentTabViewModel tab, Point position)
        {
            var page = canvas.Layout.HitTest(position.X, position.Y);
            var annotations = tab.Annotations;
            var hit = page >= 0 ? annotations.HitTest(page, canvas.ToPage(tab, page, position)) : null;
            List<Control> items;
            if (canvas.TryGetSelection(out _, out _))
            {
                items = canvas.SelectionMenu(tab);
            }
            else if (hit is not null)
            {
                annotations.Select(hit);
                items = AnnotationMenu(annotations, hit);
            }
            else if (page >= 0)
            {
                items = PageMenu(tab, new(page, canvas.ToPage(tab, page, position)));
            }
            else
            {
                return;
            }

            var menu = new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer };
            menu.Open(canvas);
            canvas.InvalidateVisual();
        }

        /// <summary>Opens a menu of a choice field's options, with the current one ticked.</summary>
        /// <param name="forms">The form state.</param>
        /// <param name="field">The field.</param>
        internal void ShowChoices(FormsViewModel forms, FormField field)
        {
            var items = new List<Control>(field.Options.Count);
            for (var i = 0; i < field.Options.Count; i++)
            {
                var item = Item(field.Options[i], null, forms.ChooseOptionCommand, new FormChoice(field, i), true);
                item.ToggleType = MenuItemToggleType.Radio;
                item.IsChecked = i == field.SelectedOption;
                items.Add(item);
            }

            new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer }.Open(canvas);
        }

        /// <summary>Matches a right-button press on the canvas.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The right-click request, or <see langword="null"/> when another button was pressed.</returns>
        private ContextMenuClick? TryMatchRightClick(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            var point = e.GetCurrentPoint(canvas);
            return point.Properties.IsRightButtonPressed ? new(canvas, tab, e, point.Position) : null;
        }

        /// <summary>Builds the menu for selected text: copy, then the ways to mark it.</summary>
        /// <param name="tab">The tab.</param>
        /// <returns>The items.</returns>
        private List<Control> SelectionMenu(DocumentTabViewModel tab)
        {
            var annotations = tab.Annotations;
            var editable = annotations.CanAnnotate;
            var mark = annotations.MarkSelectionCommand;
            var lines = canvas.GetSelectionLines();
            var readFrom = canvas.TryGetSelection(out var start, out _) ? start : default;
            var highlight = new MenuItem
            {
                Header = "_Highlight",
                IsEnabled = editable,
                ItemsSource = ColorItems(mark, lines, static (selected, color) => new MarkSelectionRequest(AnnotationKind.Highlight, color, selected)),
            };
            return
            [
                Item("_Copy", "Ctrl+C", canvas.CopyCommand, canvas.GetSelectedText(), true),
                Item("_Read Aloud from Here", null, tab.ReadAloud.ReadFromCharacterCommand, readFrom, true),
                new Separator(),
                highlight,
                Item("_Underline", null, mark, new MarkSelectionRequest(AnnotationKind.Underline, annotations.Color, lines), editable),
                Item("_Strike Out", null, mark, new MarkSelectionRequest(AnnotationKind.StrikeOut, AnnotationColors.Clay, lines), editable),
                Item("S_quiggly Underline", null, mark, new MarkSelectionRequest(AnnotationKind.Squiggly, AnnotationColors.Clay, lines), editable),
            ];
        }
    }

    /// <summary>Builds the menu for an annotation: its note, its colour and removing it.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The items.</returns>
    private static List<Control> AnnotationMenu(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        var name = AnnotationNames.Get(annotation.Kind);
        var colour = new MenuItem
        {
            Header = "C_olour",
            ItemsSource = ColorItems(annotations.RecolorAnnotationCommand, annotation, static (picked, color) => new AnnotationColorChoice(picked, color)),
        };
        List<Control> items = [new MenuItem { Header = name, IsEnabled = false }];
        if (annotation.Kind == AnnotationKind.TextBox)
        {
            // A text box's contents are its text, edited on the page rather than as a note.
            items.Add(Item("_Edit Text", "Enter", annotations.EditSelectedTextCommand, null, true));
            items.Add(Item("Text _Properties…", null, annotations.EditTextPropertiesCommand, null, true));
        }
        else
        {
            items.Add(Item(annotation.Contents.Length > 0 ? "_Edit Note" : "Add _Note", null, annotations.OpenBubbleForCommand, null, true));
        }

        items.AddRange(
        [
            Item("_Reply", null, annotations.ReplyInBubbleForCommand, null, true),
            new MenuItem { Header = "_Status", ItemsSource = StatusItems(annotations, annotation) },
            colour,
            new Separator(),
            Item($"_Delete {name}", "Delete", annotations.DeleteCommand, null, true),
        ]);
        return items;
    }

    /// <summary>Builds the menu for blank page: add a note or text where the pointer is.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="location">The page and point in page space.</param>
    /// <returns>The items.</returns>
    private static List<Control> PageMenu(DocumentTabViewModel tab, PageLocation location)
    {
        var annotations = tab.Annotations;
        var editable = annotations.CanAnnotate;
        return
        [
            Item("_Read Aloud from Here", null, tab.ReadAloud.ReadFromPointCommand, location, true),
            new Separator(),
            Item("Add _Note Here", null, annotations.BeginNoteBubbleHereCommand, location, editable),
            Item("_Type Text Here", null, annotations.TypeTextHereCommand, location, editable),
            new Separator(),
            Item("_Annotate…", null, annotations.StartCommand, null, editable),
        ];
    }

    /// <summary>Builds one item per review status.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The comment.</param>
    /// <returns>The items.</returns>
    private static List<MenuItem> StatusItems(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        var command = annotations.SetAnnotationStatusCommand;
        return
        [
            Item("_Accepted", null, command, new AnnotationStatusChoice(annotation, ReviewState.Accepted), true),
            Item("_Rejected", null, command, new AnnotationStatusChoice(annotation, ReviewState.Rejected), true),
            Item("_Cancelled", null, command, new AnnotationStatusChoice(annotation, ReviewState.Cancelled), true),
            Item("C_ompleted", null, command, new AnnotationStatusChoice(annotation, ReviewState.Completed), true),
        ];
    }

    /// <summary>Builds one named colour item per annotation colour.</summary>
    /// <typeparam name="TState">The type of what the colour applies to.</typeparam>
    /// <param name="command">The command a colour is chosen with.</param>
    /// <param name="state">What the colour applies to.</param>
    /// <param name="parameter">Makes the command parameter from the state and the colour.</param>
    /// <returns>The items.</returns>
    private static List<MenuItem> ColorItems<TState>(ICommand command, TState state, Func<TState, uint, object> parameter)
    {
        var items = new List<MenuItem>(AnnotationColors.All.Count);
        foreach (var (name, color) in AnnotationColors.All)
        {
            var swatch = new Border { Width = SwatchSize, Height = SwatchSize, CornerRadius = new(SwatchRadius), Background = new SolidColorBrush(Color.FromUInt32(0xFF000000U | color)) };
            var item = Item(name, null, command, parameter(state, color), true);
            item.Icon = swatch;
            items.Add(item);
        }

        return items;
    }

    /// <summary>Creates a menu item running a command.</summary>
    /// <param name="header">The header, with an access key.</param>
    /// <param name="gesture">The shortcut shown, or <see langword="null"/>.</param>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The command parameter, or <see langword="null"/>.</param>
    /// <param name="enabled">Whether the item can be chosen.</param>
    /// <returns>The item.</returns>
    private static MenuItem Item(string header, string? gesture, ICommand command, object? parameter, bool enabled) =>
        new() { Header = header, Command = command, CommandParameter = parameter, IsEnabled = enabled, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) };

    /// <summary>A right-button click on the page canvas that opens the context menu.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Position">The point in canvas coordinates.</param>
    internal readonly record struct ContextMenuClick(PageCanvas Canvas, DocumentTabViewModel Tab, PointerPressedEventArgs Args, Point Position)
    {
        /// <summary>Opens the context menu and marks the press handled.</summary>
        internal void OpenMenu()
        {
            Canvas.ShowContextMenu(Tab, Position);
            Args.Handled = true;
        }
    }
}
