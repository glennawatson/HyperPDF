// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Names list and tree items for screen readers. Without a name a list item reports its data object, which a screen
/// reader reads out as a type name, so each kind of item is described in words here. Each item also reports its place,
/// such as 3 of 40, where the desktop's accessibility interface carries it.
/// </summary>
internal static class ItemAutomation
{
    /// <summary>Names each item of a list as its container is prepared. Call it before the list is filled.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The subscription.</returns>
    internal static IDisposable NameItems(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);

        // The event carries only the container, and a container is not yet tied to its list while it is prepared,
        // so the list travels with each event to count its items.
        return list.ContainersPrepared
            .SelectWith(list, static (owner, args) => new PreparedItem(owner, args))
            .SubscribeSafe(NameItem, ErrorTrace.Write);
    }

    /// <summary>Names the list or tree item that shows a view.</summary>
    /// <param name="view">The item's view.</param>
    /// <param name="name">The name.</param>
    internal static void NameContainer(Control view, string? name)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var ancestor in view.GetVisualAncestors())
        {
            if (ancestor is not (ListBoxItem or TreeViewItem))
            {
                continue;
            }

            AutomationProperties.SetName((Control)ancestor, name);
            return;
        }
    }

    /// <summary>Names a list item as it is prepared for an item, from what the item holds and where it sits.</summary>
    /// <param name="prepared">The list and the container that was prepared with its position.</param>
    private static void NameItem(PreparedItem prepared)
    {
        var (list, args) = prepared;
        var container = args.Container;
        var item = list.ItemFromContainer(container) ?? container.DataContext;
        var count = list.Items.Count;
        AutomationProperties.SetName(container, ItemDescriptions.Describe(item, args.Index, count));
        AutomationProperties.SetPositionInSet(container, args.Index + 1);
        AutomationProperties.SetSizeOfSet(container, count);

        // Avalonia's Linux and macOS bridges drop the set position, so there it is spoken as the description.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            AutomationProperties.SetHelpText(container, ItemDescriptions.DescribePosition(item, args.Index, count));
        }
    }
}
