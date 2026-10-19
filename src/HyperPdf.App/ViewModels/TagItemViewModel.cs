// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;

namespace HyperPdf.App.ViewModels;

/// <summary>One row of the tags list: a tag, how far to indent it, and how it reads aloud.</summary>
/// <param name="Tag">The tag.</param>
[DebuggerDisplay("TagItemViewModel: {Row}")]
public sealed record TagItemViewModel(DocumentTag Tag)
{
    /// <summary>The indent of each level of nesting, in pixels.</summary>
    private const double IndentStep = 14;

    /// <summary>Gets the tag's object number.</summary>
    public int Id => Tag.Id;

    /// <summary>Gets the line the row shows.</summary>
    public string Row { get; } = TagText.Row(Tag);

    /// <summary>Gets how a screen reader says the row.</summary>
    public string SpokenName { get; } = TagText.Speak(Tag);

    /// <summary>Gets the room to the left of the row that shows how deep the tag is.</summary>
    public Avalonia.Thickness Indent { get; } = new(Tag.Depth * IndentStep, 0, 0, 0);

    /// <summary>Lists the rows for tags.</summary>
    /// <param name="tags">The tags in reading order.</param>
    /// <returns>The rows, in the same order.</returns>
    public static IReadOnlyList<TagItemViewModel> Create(IReadOnlyList<DocumentTag> tags)
    {
        var items = new TagItemViewModel[tags.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new(tags[i]);
        }

        return items;
    }
}
