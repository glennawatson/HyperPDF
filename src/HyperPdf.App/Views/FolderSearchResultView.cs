// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Views;

/// <summary>Builds the small view of one folder search result: where it is, then the words around it.</summary>
internal static class FolderSearchResultView
{
    /// <summary>The gap between the location and the snippet.</summary>
    private const double Spacing = 2;

    /// <summary>The most lines of snippet shown.</summary>
    private const int SnippetLines = 2;

    /// <summary>Creates the view for a result.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The view.</returns>
    internal static Control Create(FolderSearchResultViewModel? result)
    {
        var location = new TextBlock { Text = result?.Location, FontWeight = FontWeight.SemiBold };
        var snippet = new TextBlock { Text = result?.Snippet, TextWrapping = TextWrapping.Wrap, MaxLines = SnippetLines, TextTrimming = TextTrimming.CharacterEllipsis };
        if (result is { IsMatch: false })
        {
            snippet.Classes.Add("secondary");
        }

        var panel = new StackPanel { Spacing = Spacing, Orientation = Orientation.Vertical, Children = { location, snippet } };
        AutomationProperties.SetName(panel, result?.SpokenText);
        return panel;
    }
}
