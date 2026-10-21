// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>An outline (bookmark) entry in the sidebar.</summary>
[DebuggerDisplay("OutlineItemViewModel: {Title}")]
public sealed partial class OutlineItemViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="OutlineItemViewModel"/> class.</summary>
    /// <param name="node">The outline node.</param>
    public OutlineItemViewModel(OutlineNode node)
        : this(node, true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OutlineItemViewModel"/> class.</summary>
    /// <param name="node">The outline node.</param>
    /// <param name="ignoreColours">Whether the colour the document gives the title is left out, so the title has the theme's text colour.</param>
    public OutlineItemViewModel(OutlineNode node, bool ignoreColours)
    {
        ArgumentNullException.ThrowIfNull(node);
        Title = string.IsNullOrWhiteSpace(node.Title) ? "(untitled)" : node.Title;
        Target = node.Target;
        Id = node.Id;
        IsExpanded = node.IsOpen;
        IsBold = node.Style?.Bold ?? false;
        IsItalic = node.Style?.Italic ?? false;
        Colour = ignoreColours ? null : node.Style?.Color;
        var children = new OutlineItemViewModel[node.Children.Count];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = new(node.Children[i], ignoreColours);
        }

        Children = children;
    }

    /// <summary>Gets a value indicating whether the document asks for the title in bold.</summary>
    public bool IsBold { get; }

    /// <summary>Gets a value indicating whether the document asks for the title in italics.</summary>
    public bool IsItalic { get; }

    /// <summary>Gets the colour the title is shown in as 0xRRGGBB, or <see langword="null"/> for the theme's text colour.</summary>
    public uint? Colour { get; }

    /// <summary>Gets the entry's object number, which the bookmark editor names it by; 0 when it cannot be edited.</summary>
    public int Id { get; }

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the navigation target.</summary>
    public LinkTarget Target { get; }

    /// <summary>Gets the child entries.</summary>
    public IReadOnlyList<OutlineItemViewModel> Children { get; }

    /// <summary>Gets or sets a value indicating whether the entry is expanded.</summary>
    [Reactive]
    public partial bool IsExpanded { get; set; }

    /// <summary>Collects the object numbers of the entries that are expanded, so a rebuilt tree can open the same ones.</summary>
    /// <param name="items">The entries.</param>
    /// <param name="expanded">Receives the numbers.</param>
    public static void CollectExpanded(IReadOnlyList<OutlineItemViewModel> items, HashSet<int> expanded)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(expanded);
        foreach (var item in items)
        {
            if (item.IsExpanded)
            {
                _ = expanded.Add(item.Id);
            }

            CollectExpanded(item.Children, expanded);
        }
    }

    /// <summary>Expands the entries whose numbers were collected.</summary>
    /// <param name="items">The entries.</param>
    /// <param name="expanded">The numbers of the entries to expand.</param>
    public static void ApplyExpanded(IReadOnlyList<OutlineItemViewModel> items, HashSet<int> expanded)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(expanded);
        foreach (var item in items)
        {
            item.IsExpanded = expanded.Contains(item.Id);
            ApplyExpanded(item.Children, expanded);
        }
    }

    /// <summary>Finds an entry by its object number.</summary>
    /// <param name="items">The entries.</param>
    /// <param name="id">The object number.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public static OutlineItemViewModel? Find(IReadOnlyList<OutlineItemViewModel> items, int id)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (id <= 0)
        {
            return null;
        }

        foreach (var item in items)
        {
            if (item.Id == id)
            {
                return item;
            }

            if (Find(item.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Converts outline nodes to view models.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The view models.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<OutlineItemViewModel> Create(IReadOnlyList<OutlineNode>? nodes) => Create(nodes, true);

    /// <summary>Converts outline nodes to view models.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="ignoreColours">Whether the colours the document gives the titles are left out.</param>
    /// <returns>The view models.</returns>
    public static IReadOnlyList<OutlineItemViewModel> Create(IReadOnlyList<OutlineNode>? nodes, bool ignoreColours)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return [];
        }

        var items = new OutlineItemViewModel[nodes.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new(nodes[i], ignoreColours);
        }

        return items;
    }
}
