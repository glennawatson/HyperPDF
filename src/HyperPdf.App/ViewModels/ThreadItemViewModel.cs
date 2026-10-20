// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdf.Core.Documents;
using ReactiveUI;

namespace HyperPdf.App.ViewModels;

/// <summary>An article thread in the parts panel: its title and the places it is read, in reading order.</summary>
[DebuggerDisplay("ThreadItemViewModel: {Title} {Beads.Count} beads")]
public sealed class ThreadItemViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="ThreadItemViewModel"/> class.</summary>
    /// <param name="thread">The thread.</param>
    public ThreadItemViewModel(ArticleThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        Title = thread.Title;
        Beads = thread.Beads;
        Summary = string.Create(CultureInfo.CurrentCulture, $"{Beads.Count} {(Beads.Count == 1 ? "place" : "places")}");
        SpokenName = $"{Title}, {Summary}";
    }

    /// <summary>Gets the article's title.</summary>
    public string Title { get; }

    /// <summary>Gets the places the article is read, in reading order.</summary>
    public IReadOnlyList<ArticleBead> Beads { get; }

    /// <summary>Gets how many places the article is read in, in words.</summary>
    public string Summary { get; }

    /// <summary>Gets how a screen reader says the entry.</summary>
    public string SpokenName { get; }
}
