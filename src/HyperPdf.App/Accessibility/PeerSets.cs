// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>
/// Works out the place of list items, tabs, tree items, menu items and radio buttons among their neighbours, so a screen reader can
/// say "2 of 7". A peer that already knows its own place keeps it.
/// </summary>
internal static class PeerSets
{
    /// <summary>Gives each run of neighbouring elements of the same set kind their places.</summary>
    /// <param name="children">The numbers of the children of one element, in order.</param>
    /// <param name="nodes">The elements built so far; the children's entries are replaced.</param>
    internal static void Assign(List<int> children, Dictionary<int, AccessibleNode> nodes)
    {
        var index = 0;
        while (index < children.Count)
        {
            var role = nodes[children[index]].Role;
            var end = index + 1;
            while (IsSetRole(role) && end < children.Count && nodes[children[end]].Role == role)
            {
                end++;
            }

            if (IsSetRole(role))
            {
                Number(children, nodes, index, end);
            }

            index = end;
        }
    }

    /// <summary>Checks whether elements of a role are counted as a set.</summary>
    /// <param name="role">The role.</param>
    /// <returns>Whether they are.</returns>
    private static bool IsSetRole(AccessibleRole role) =>
        role is AccessibleRole.ListItem or AccessibleRole.TabItem or AccessibleRole.TreeItem or AccessibleRole.MenuItem or AccessibleRole.RadioButton;

    /// <summary>Numbers one run of elements.</summary>
    /// <param name="children">The numbers of the children.</param>
    /// <param name="nodes">The elements built so far.</param>
    /// <param name="start">The index of the run's first child.</param>
    /// <param name="end">The index after the run's last child.</param>
    private static void Number(List<int> children, Dictionary<int, AccessibleNode> nodes, int start, int end)
    {
        var size = end - start;
        for (var index = start; index < end; index++)
        {
            var node = nodes[children[index]];
            if (node.PositionInSet == 0)
            {
                nodes[node.Id] = node with { PositionInSet = index - start + 1, SetSize = size };
            }
        }
    }
}
