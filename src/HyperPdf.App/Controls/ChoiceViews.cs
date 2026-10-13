// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HyperPdf.App.Controls;

/// <summary>Builds the view of one choice in a list of choices that each have a name and a description.</summary>
internal static class ChoiceViews
{
    /// <summary>The vertical space between a choice's name and its description.</summary>
    private const double ChoiceGap = 2;

    /// <summary>The vertical space around a choice.</summary>
    private const double ChoicePadding = 4;

    /// <summary>Creates the view of one choice: its name, and under it what it means.</summary>
    /// <param name="name">The choice's name.</param>
    /// <param name="description">What the choice means.</param>
    /// <returns>The view.</returns>
    internal static StackPanel Create(string? name, string? description)
    {
        var title = new TextBlock { Text = name, FontWeight = FontWeight.SemiBold };
        var detail = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap };
        detail.Classes.Add("secondary");
        return new StackPanel { Spacing = ChoiceGap, Margin = new(0, ChoicePadding), HorizontalAlignment = HorizontalAlignment.Stretch, Children = { title, detail } };
    }
}
