// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.ViewModels.Services;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Makes the rows of a window described as a table, and turns a table into the window: its layout and its bindings to the view model.</summary>
internal static class FormSettings
{
    /// <summary>Makes the name of a group of rows.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="label">The name in English, which is looked up in the language shown.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormSetting<T> Heading<T>(string label) => Row<T>(FormRowKind.Heading, _ => UiText.Get(label));

    /// <summary>Makes a row of text read from the view model. The row is left out while the text is empty.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="text">Reads the text.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormSetting<T> Text<T>(Func<T, string?> text) => Row<T>(FormRowKind.Text, model => text(model) ?? string.Empty);

    /// <summary>Makes a row of fixed text.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="text">The text in English, which is looked up in the language shown.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormSetting<T> Note<T>(string text) => Row<T>(FormRowKind.Text, _ => UiText.Get(text));

    /// <summary>Makes a setting that is on or off.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads whether it is on.</param>
    /// <param name="set">Turns it on or off.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Toggle<T>(string label, Func<T, bool> get, Action<T, bool> set) =>
        Row<T>(FormRowKind.Toggle, _ => UiText.Get(label)) with { Selected = model => get(model) ? 1 : 0, Apply = (model, edit) => set(model, edit.On) };

    /// <summary>Makes a setting typed as text.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads the text.</param>
    /// <param name="set">Writes the text.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Field<T>(string label, Func<T, string> get, Action<T, string> set) =>
        Row<T>(FormRowKind.Field, _ => UiText.Get(label)) with { Value = get, Apply = (model, edit) => set(model, edit.Text) };

    /// <summary>Makes a setting chosen from a fixed list of names by its place in the list.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="label">The name in English.</param>
    /// <param name="options">The values, already in the language shown.</param>
    /// <param name="get">Reads the chosen value.</param>
    /// <param name="set">Writes the chosen value.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Choice<T>(string label, IReadOnlyList<string> options, Func<T, int> get, Action<T, int> set) =>
        Row<T>(FormRowKind.Choice, _ => UiText.Get(label)) with { Options = _ => options, Selected = get, Apply = (model, edit) => set(model, edit.Index) };

    /// <summary>Makes a setting chosen from a list of the view model's own choices.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <typeparam name="TChoice">The kind of choice.</typeparam>
    /// <param name="label">The name in English.</param>
    /// <param name="choices">Reads the choices.</param>
    /// <param name="name">Reads the name of a choice, in the language shown.</param>
    /// <param name="get">Reads the chosen one.</param>
    /// <param name="set">Writes the chosen one.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Pick<T, TChoice>(string label, Func<T, IReadOnlyList<TChoice>> choices, Func<TChoice, string> name, Func<T, TChoice?> get, Action<T, TChoice> set)
        where TChoice : class =>
        Row<T>(FormRowKind.Choice, _ => UiText.Get(label)) with
        {
            Options = model => NamesOf(choices(model), name),
            Selected = model => IndexOf(choices(model), get(model)),
            Apply = (model, edit) =>
            {
                var list = choices(model);
                if ((uint)edit.Index < (uint)list.Count)
                {
                    set(model, list[edit.Index]);
                }
            },
        };

    /// <summary>Makes a list: one line for each item the view model holds. With a command, a press on a line runs the command on its item.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <typeparam name="TItem">The kind of item.</typeparam>
    /// <param name="items">Reads the items.</param>
    /// <param name="text">Reads the text of an item.</param>
    /// <param name="command">Finds the command a press on an item runs, or <see langword="null"/> for a list that is only read.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> List<T, TItem>(Func<T, IReadOnlyList<TItem>> items, Func<TItem, string> text, Func<T, ICommand>? command)
        where TItem : class =>
        Row<T>(command is null ? FormRowKind.Text : FormRowKind.Action, static _ => string.Empty) with
        {
            Items = items,
            ItemText = item => item is TItem typed ? text(typed) : string.Empty,
            Command = command,
        };

    /// <summary>Makes a line of words that the view model can change, such as how a download is going.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="text">Reads the words.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormSetting<T> Status<T>(Func<T, string> text) => Row(FormRowKind.Text, text);

    /// <summary>Makes a list to choose from: one line for each item the view model holds, and a press on a line hands its item to the view model.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <typeparam name="TItem">The kind of item.</typeparam>
    /// <param name="items">Reads the items.</param>
    /// <param name="text">Reads the text of an item.</param>
    /// <param name="pick">Takes the item of the line that was pressed.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Picker<T, TItem>(Func<T, IReadOnlyList<TItem>> items, Func<TItem, string> text, Action<T, TItem> pick)
        where TItem : class =>
        Row<T>(FormRowKind.Action, static _ => string.Empty) with
        {
            Items = items,
            ItemText = item => item is TItem typed ? text(typed) : string.Empty,
            Apply = (model, edit) =>
            {
                if (edit.Target is TItem picked)
                {
                    pick(model, picked);
                }
            },
        };

    /// <summary>Makes something to do, which runs a command of the view model when it can run.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="label">What it does, in English.</param>
    /// <param name="command">Finds the command.</param>
    /// <returns>The row.</returns>
    internal static FormSetting<T> Action<T>(string label, Func<T, ICommand> command) =>
        Row<T>(FormRowKind.Action, _ => UiText.Get(label)) with { Command = command, Enabled = model => command(model).CanExecute(null) };

    /// <summary>Turns a table of rows into a window over a view model.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="title">The heading.</param>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The rows, top to bottom.</param>
    /// <param name="changes">Signals each change the view model makes by itself.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest Window<T>(string title, T model, FormSetting<T>[] rows, IObservable<RxVoid> changes) => new(
        title,
        form => Describe(form, model, rows),
        null,
        null,
        changes,
        null,
        edits => Bind(edits, model, rows));

    /// <summary>Turns a table of rows into a window over a view model, laid out again whenever any property of the view model changes.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="title">The heading.</param>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The rows, top to bottom.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest Window<T>(string title, T model, FormSetting<T>[] rows)
        where T : class, IReactiveObject =>
        Window(title, model, rows, model.GetChangedObservable().AsSignal());

    /// <summary>Adds the rows that are shown now, reading each one from the view model.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="form">Receives the rows.</param>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The rows of the window.</param>
    private static void Describe<T>(FormBuilder form, T model, FormSetting<T>[] rows)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (!row.Visible(model))
            {
                continue;
            }

            if (row.Items is { } items)
            {
                AddList(form, row, items(model), i);
            }
            else
            {
                Add(form, model, row, i);
            }
        }
    }

    /// <summary>Adds the lines of a list.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="form">Receives the lines.</param>
    /// <param name="row">The row that stands for the list.</param>
    /// <param name="items">The items.</param>
    /// <param name="id">The row's place in the table, which a press on a line comes back with.</param>
    private static void AddList<T>(FormBuilder form, FormSetting<T> row, IReadOnlyList<object> items, int id)
    {
        foreach (var item in items)
        {
            if (row.Kind == FormRowKind.Text)
            {
                form.Text(row.ItemText(item));
            }
            else
            {
                form.Action(row.ItemText(item), true, id, item);
            }
        }
    }

    /// <summary>Adds one row.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="form">Receives the row.</param>
    /// <param name="model">The view model.</param>
    /// <param name="row">The row.</param>
    /// <param name="id">The row's place in the table, which its changes come back with.</param>
    private static void Add<T>(FormBuilder form, T model, FormSetting<T> row, int id)
    {
        var label = row.Label(model);
        switch (row.Kind)
        {
            case FormRowKind.Heading:
                {
                    form.Heading(label);
                    break;
                }

            case FormRowKind.Text:
                {
                    form.Text(label);
                    break;
                }

            case FormRowKind.Toggle:
                {
                    form.Toggle(label, row.Selected(model) != 0, id);
                    break;
                }

            case FormRowKind.Choice:
                {
                    form.Choice(label, row.Options(model), row.Selected(model), id);
                    break;
                }

            case FormRowKind.Field:
                {
                    form.Field(label, row.Value(model), id);
                    break;
                }

            default:
                {
                    form.Action(label, row.Enabled(model), id, null);
                    break;
                }
        }
    }

    /// <summary>Follows the changes the user makes: a setting is written to the view model, and an action runs its command.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="edits">The changes the user makes in the window.</param>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The rows of the window.</param>
    /// <returns>What ends the following.</returns>
    private static MultipleDisposable Bind<T>(IObservable<FormEdit> edits, T model, FormSetting<T>[] rows)
    {
        MultipleDisposable bound = [edits.SubscribeSafe(edit => Write(model, rows, edit), ErrorTrace.Write)];
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Command is not { } command)
            {
                continue;
            }

            // A line of a list runs the command on its item. Any other action runs it with nothing.
            bound.Add(rows[i].Items is null
                ? edits.WhereRow(i).AsSignal().InvokeCommand(command(model))
                : edits.WhereRow(i).Select(static edit => edit.Target).KeepNotNull().InvokeCommand(command(model)));
        }

        return bound;
    }

    /// <summary>Writes a change to the view model through the row it belongs to.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="model">The view model.</param>
    /// <param name="rows">The rows of the window.</param>
    /// <param name="edit">The change.</param>
    private static void Write<T>(T model, FormSetting<T>[] rows, FormEdit edit)
    {
        if ((uint)edit.Id < (uint)rows.Length && rows[edit.Id].Enabled(model))
        {
            rows[edit.Id].Apply(model, edit);
        }
    }

    /// <summary>Makes a row that reads only its text.</summary>
    /// <typeparam name="T">The kind of view model the window shows.</typeparam>
    /// <param name="kind">What the row is.</param>
    /// <param name="label">Reads the row's text.</param>
    /// <returns>The row.</returns>
    private static FormSetting<T> Row<T>(FormRowKind kind, Func<T, string> label) =>
        new(kind, label, static _ => [], static _ => 0, static _ => string.Empty, static (_, _) => { }, null, static _ => true, static _ => true);

    /// <summary>Lists the names of some choices.</summary>
    /// <typeparam name="TChoice">The kind of choice.</typeparam>
    /// <param name="choices">The choices.</param>
    /// <param name="name">Reads the name of a choice.</param>
    /// <returns>The names, in the order of the choices.</returns>
    private static string[] NamesOf<TChoice>(IReadOnlyList<TChoice> choices, Func<TChoice, string> name)
    {
        var names = new string[choices.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = name(choices[i]);
        }

        return names;
    }

    /// <summary>Finds a choice in a list.</summary>
    /// <typeparam name="TChoice">The kind of choice.</typeparam>
    /// <param name="choices">The choices.</param>
    /// <param name="chosen">The chosen one, or <see langword="null"/>.</param>
    /// <returns>Its place, or -1.</returns>
    private static int IndexOf<TChoice>(IReadOnlyList<TChoice> choices, TChoice? chosen)
        where TChoice : class
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (ReferenceEquals(choices[i], chosen))
            {
                return i;
            }
        }

        return -1;
    }
}
