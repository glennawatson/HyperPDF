// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>What the right-click menus hold, worded and wired here so every render head offers the same choices.</summary>
public static class ContextMenus
{
    /// <summary>Builds the menu for selected text: copy, then the ways to mark it.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="copy">The command that copies text.</param>
    /// <param name="selectedText">The selected text.</param>
    /// <param name="lines">The selected lines of each page.</param>
    /// <param name="readFrom">Where reading aloud would start.</param>
    /// <returns>The entries.</returns>
    public static List<MenuEntry> ForSelection(DocumentTabViewModel tab, ICommand copy, string selectedText, IReadOnlyDictionary<int, List<PageRect>> lines, CharacterPosition readFrom)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(lines);
        var annotations = tab.Annotations;
        var editable = annotations.CanAnnotate;
        var mark = annotations.MarkSelectionCommand;
        return
        [

            MenuEntry.Item(UiText.Get("_Copy"), "Ctrl+C", copy, selectedText, true),
            MenuEntry.Item(UiText.Get("_Read Aloud from Here"), null, tab.ReadAloud.ReadFromCharacterCommand, readFrom, true),
            MenuEntry.Separator(),
            MenuEntry.Submenu(
                UiText.Get("_Highlight"),
                editable,
                ColorItems(mark, lines, static (selected, color) => new MarkSelectionRequest(AnnotationKind.Highlight, color, selected))),
            MenuEntry.Item(UiText.Get("_Underline"), null, mark, new MarkSelectionRequest(AnnotationKind.Underline, annotations.Color, lines), editable),
            MenuEntry.Item(UiText.Get("_Strike Out"), null, mark, new MarkSelectionRequest(AnnotationKind.StrikeOut, AnnotationColors.Clay, lines), editable),
            MenuEntry.Item(UiText.Get("S_quiggly Underline"), null, mark, new MarkSelectionRequest(AnnotationKind.Squiggly, AnnotationColors.Clay, lines), editable)
        ];
    }

    /// <summary>Builds the menu for an annotation: its note, its colour and removing it.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The entries.</returns>
    public static List<MenuEntry> ForAnnotation(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(annotation);
        var name = AnnotationNames.Get(annotation.Kind);
        List<MenuEntry> items = [MenuEntry.Title(name)];
        if (annotation.Kind == AnnotationKind.TextBox)
        {
            // A text box's contents are its text, edited on the page rather than as a note.
            items.Add(MenuEntry.Item(UiText.Get("_Edit Text"), "Enter", annotations.EditSelectedTextCommand, null, true));
            items.Add(MenuEntry.Item(UiText.Get("Text _Properties…"), null, annotations.EditTextPropertiesCommand, null, true));
        }
        else
        {
            items.Add(
                MenuEntry.Item(annotation.Contents.Length > 0 ? UiText.Get("_Edit Note") : UiText.Get("Add _Note"), null, annotations.OpenBubbleForCommand, null, true));
        }

        items.AddRange(
            [

            MenuEntry.Item(UiText.Get("_Reply"), null, annotations.ReplyInBubbleForCommand, null, true),
            MenuEntry.Submenu(UiText.Get("_Status"), true, StatusItems(annotations, annotation)),
            MenuEntry.Submenu(
                    UiText.Get("C_olour"),
                    true,
                    ColorItems(annotations.RecolorAnnotationCommand, annotation, static (picked, color) => new AnnotationColorChoice(picked, color))),
            MenuEntry.Separator(),
            MenuEntry.Item(UiText.Format("_Delete {0}", name), "Delete", annotations.DeleteCommand, null, true)
        ]);
        return items;
    }

    /// <summary>Builds the menu for a blank page: add a note or text where the pointer is.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="location">The page and point in page space.</param>
    /// <returns>The entries.</returns>
    public static List<MenuEntry> ForPage(DocumentTabViewModel tab, PageLocation location)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var annotations = tab.Annotations;
        var editable = annotations.CanAnnotate;
        return
        [

            MenuEntry.Item(UiText.Get("_Read Aloud from Here"), null, tab.ReadAloud.ReadFromPointCommand, location, true),
            MenuEntry.Separator(),
            MenuEntry.Item(UiText.Get("Add _Note Here"), null, annotations.BeginNoteBubbleHereCommand, location, editable),
            MenuEntry.Item(UiText.Get("_Type Text Here"), null, annotations.TypeTextHereCommand, location, editable),
            MenuEntry.Separator(),
            MenuEntry.Item(UiText.Get("_Annotate…"), null, annotations.StartCommand, null, editable)
        ];
    }

    /// <summary>Builds the menu of a choice field's options, with the current one ticked.</summary>
    /// <param name="forms">The form state.</param>
    /// <param name="field">The field.</param>
    /// <returns>The entries.</returns>
    public static List<MenuEntry> ForChoices(FormsViewModel forms, FormField field)
    {
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(field);
        var items = new List<MenuEntry>(field.Options.Count);
        for (var i = 0; i < field.Options.Count; i++)
        {
            items.Add(MenuEntry.Radio(field.Options[i], forms.ChooseOptionCommand, new FormChoice(field, i), i == field.SelectedOption));
        }

        return items;
    }

    /// <summary>Builds one item per review status.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The comment.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> StatusItems(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        var command = annotations.SetAnnotationStatusCommand;
        return
        [

            MenuEntry.Item(UiText.Get("_Accepted"), null, command, new AnnotationStatusChoice(annotation, ReviewState.Accepted), true),
            MenuEntry.Item(UiText.Get("_Rejected"), null, command, new AnnotationStatusChoice(annotation, ReviewState.Rejected), true),
            MenuEntry.Item(UiText.Get("_Cancelled"), null, command, new AnnotationStatusChoice(annotation, ReviewState.Cancelled), true),
            MenuEntry.Item(UiText.Get("C_ompleted"), null, command, new AnnotationStatusChoice(annotation, ReviewState.Completed), true)
        ];
    }

    /// <summary>Builds one named colour item per annotation colour.</summary>
    /// <typeparam name="TState">The type of what the colour applies to.</typeparam>
    /// <param name="command">The command a colour is chosen with.</param>
    /// <param name="state">What the colour applies to.</param>
    /// <param name="parameter">Makes the command parameter from the state and the colour.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> ColorItems<TState>(ICommand command, TState state, Func<TState, uint, object> parameter)
    {
        var items = new List<MenuEntry>(AnnotationColors.All.Count);
        foreach (var (name, color) in AnnotationColors.All)
        {
            items.Add(MenuEntry.Item(UiText.Get(name), null, command, parameter(state, color), true).WithSwatch(color));
        }

        return items;
    }
}
