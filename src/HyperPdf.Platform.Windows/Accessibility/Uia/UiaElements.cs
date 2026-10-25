// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// Holds the provider of each element so UI Automation gets the same object for the same element, and finds the root of a window.
/// UI Automation calls from its own threads, so every method is safe to call from any thread.
/// </summary>
[DebuggerDisplay("UiaElements: {_elements.Count} providers")]
internal sealed class UiaElements
{
    /// <summary>The providers by element number.</summary>
    private readonly Dictionary<int, UiaElement> _elements = [];

    /// <summary>Guards the providers.</summary>
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="UiaElements"/> class.</summary>
    /// <param name="tree">The elements to serve.</param>
    internal UiaElements(IAccessibleTree tree) => Tree = tree;

    /// <summary>Gets the elements being served.</summary>
    internal IAccessibleTree Tree { get; }

    /// <summary>Gets the provider of an element, making it the first time.</summary>
    /// <param name="id">The element's number.</param>
    /// <returns>The provider.</returns>
    internal UiaElement Get(int id)
    {
        lock (_gate)
        {
            ref var element = ref CollectionsMarshal.GetValueRefOrAddDefault(_elements, id, out _);
            return element ??= new(this, id);
        }
    }

    /// <summary>Drops the provider of an element that has gone.</summary>
    /// <param name="id">The element's number.</param>
    internal void Forget(int id)
    {
        lock (_gate)
        {
            _ = _elements.Remove(id);
        }
    }

    /// <summary>Finds the provider of the element that stands for a window.</summary>
    /// <param name="window">The window's number.</param>
    /// <returns>The provider, or <see langword="null"/> when the tree has no such window.</returns>
    internal UiaElement? RootOfWindow(long window)
    {
        if (Tree.Get(Tree.RootId) is not { } application)
        {
            return null;
        }

        foreach (var id in application.Children)
        {
            if (Tree.Get(id) is { } candidate && candidate.WindowNumber == window)
            {
                return Get(id);
            }
        }

        return null;
    }

    /// <summary>Finds the provider of the window an element is in.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The window's provider, or <see langword="null"/> when the element is not in a window.</returns>
    internal UiaElement? RootOf(AccessibleNode node)
    {
        var current = node;
        while (current.ParentId != Tree.RootId)
        {
            if (current.ParentId < 0 || Tree.Get(current.ParentId) is not { } parent)
            {
                return null;
            }

            current = parent;
        }

        return Get(current.Id);
    }
}
