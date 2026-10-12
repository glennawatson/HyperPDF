// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>An outline (bookmark) entry in the sidebar.</summary>
[DebuggerDisplay("OutlineItemViewModel: {Title}")]
public sealed partial class OutlineItemViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="OutlineItemViewModel"/> class.</summary>
    /// <param name="node">The outline node.</param>
    public OutlineItemViewModel(OutlineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Title = string.IsNullOrWhiteSpace(node.Title) ? "(untitled)" : node.Title;
        Target = node.Target;
        IsExpanded = node.IsOpen;
        var children = new OutlineItemViewModel[node.Children.Count];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = new(node.Children[i]);
        }

        Children = children;
    }

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the navigation target.</summary>
    public LinkTarget Target { get; }

    /// <summary>Gets the child entries.</summary>
    public IReadOnlyList<OutlineItemViewModel> Children { get; }

    /// <summary>Gets or sets a value indicating whether the entry is expanded.</summary>
    [Reactive]
    public partial bool IsExpanded { get; set; }

    /// <summary>Converts outline nodes to view models.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The view models.</returns>
    public static IReadOnlyList<OutlineItemViewModel> Create(IReadOnlyList<OutlineNode>? nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return [];
        }

        var items = new OutlineItemViewModel[nodes.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new(nodes[i]);
        }

        return items;
    }
}
