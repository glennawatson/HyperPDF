// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>One thing the accessibility check found, worded for a list.</summary>
/// <param name="Problem">The problem.</param>
[DebuggerDisplay("TagProblemItem: {Text}")]
public sealed record TagProblemItem(DocumentTagProblem Problem) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the tag the problem is about; 0 when there is none to select.</summary>
    public int TagId => Problem.TagId;

    /// <summary>Gets the sentence, led by the page when the problem is on one.</summary>
    public string Text => Problem.PageIndex >= 0
        ? UiText.Format(
        "Page {0}: {1}",
        Problem.PageIndex + 1,
        Problem.Message)
        : Problem.Message;

    /// <summary>Lists the rows for problems.</summary>
    /// <param name="problems">The problems.</param>
    /// <returns>The rows, in the same order.</returns>
    public static IReadOnlyList<TagProblemItem> Create(IReadOnlyList<DocumentTagProblem> problems)
    {
        var items = new TagProblemItem[problems.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new(problems[i]);
        }

        return items;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TagProblemItem? other) => other is not null && Problem.Equals(other.Problem);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Problem.GetHashCode();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
}
