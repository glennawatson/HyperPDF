// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using CP.Localisation.Avalonia;

namespace HyperPdf.App.Controls;

/// <summary>Builds the view of one choice in a list of choices that each have a name and a description.</summary>
internal static class ChoiceViews
{
    /// <summary>The vertical space between a choice's name and its description.</summary>
    private const double ChoiceGap = 2;

    /// <summary>The vertical space around a choice.</summary>
    private const double ChoicePadding = 4;

    /// <summary>Shows translated labels over stable key items so language changes preserve selection.</summary>
    /// <param name="control">The selector whose existing index binding remains authoritative.</param>
    /// <param name="choices">The application-defined choices.</param>
    internal static void SetChoices(ComboBox control, IReadOnlyList<string> choices)
    {
        control.ItemsSource = LocalisationChoices.CreateItems(UiText, LocalisationStringList.GetResourceKeys(choices));
        control.ItemTemplate = CreateKeysTemplate();
    }

    /// <summary>Creates a culture-aware presentation for immutable resource-key items.</summary>
    /// <returns>The translated label template.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IDataTemplate CreateKeysTemplate() => new FuncDataTemplate<LocalisationChoiceItem>(
        static (choice, _) => CreateLabel(choice, static item => item?.ToString()));

    /// <summary>Creates a binding-owned converter that retains stable item identity for each choice list.</summary>
    /// <returns>The converter, owned by its view binding.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Func<IReadOnlyList<string>, IEnumerable> CreateItemsConverter()
    {
        var converter = LocalisationChoices.CreateItemsConverter(UiText);

        // Each view binding owns its converter; the adapter retains the application's stable key-list identity.
        return choices => converter(LocalisationStringList.GetResourceKeys(choices));
    }

    /// <summary>Creates the view of one choice: its name, and under it what it means.</summary>
    /// <typeparam name="TChoice">The immutable choice data.</typeparam>
    /// <param name="choice">The choice displayed by this view.</param>
    /// <param name="name">Reads the choice's translated name.</param>
    /// <param name="description">Reads its translated explanation.</param>
    /// <returns>The view.</returns>
    internal static StackPanel Create<TChoice>(TChoice choice, Func<TChoice, string?> name, Func<TChoice, string?> description)
    {
        var title = CreateLabel(choice, name);
        title.FontWeight = FontWeight.SemiBold;
        var detail = CreateLabel(choice, description);
        detail.Classes.Add("secondary");
        return new StackPanel { Spacing = ChoiceGap, Margin = new(0, ChoicePadding), HorizontalAlignment = HorizontalAlignment.Stretch, Children = { title, detail } };
    }

    /// <summary>Builds a label that resolves its typed data again when the interface language changes.</summary>
    /// <typeparam name="TChoice">The data shown by the label.</typeparam>
    /// <param name="choice">The existing data object.</param>
    /// <param name="text">Reads its current presentation text.</param>
    /// <returns>The culture-bound label.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextBlock CreateLabel<TChoice>(TChoice choice, Func<TChoice, string?> text) => new()
    {
        TextWrapping = TextWrapping.Wrap,
        [!TextBlock.TextProperty] = LocalisationBinding.Create(UiText, new LabelState<TChoice>(choice, text), static (state, _) => state.Text(state.Choice)),
    };

    /// <summary>Owns the typed data and text selector of one culture binding.</summary>
    /// <typeparam name="TChoice">The existing presentation data.</typeparam>
    /// <param name="Choice">The data object.</param>
    /// <param name="Text">Reads its current text.</param>
    [DebuggerDisplay("LabelState: {Choice}")]
    private sealed record LabelState<TChoice>(TChoice Choice, Func<TChoice, string?> Text);
}
