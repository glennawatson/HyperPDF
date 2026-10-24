// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>Decides which UI Automation patterns an element offers, from the actions and text it lists.</summary>
internal static class UiaPatterns
{
    /// <summary>Checks whether an element offers a pattern.</summary>
    /// <param name="pattern">The pattern, a <see cref="UiaPatternId"/>.</param>
    /// <param name="node">The element.</param>
    /// <returns>Whether it does.</returns>
    internal static bool Offers(int pattern, AccessibleNode node) =>
        (UiaPatternId)pattern switch
        {
            UiaPatternId.Invoke => HasAction(node, AccessibleActions.Click),
            UiaPatternId.Toggle => HasAction(node, AccessibleActions.Toggle),
            UiaPatternId.ExpandCollapse => HasAction(node, AccessibleActions.Expand) || HasAction(node, AccessibleActions.Collapse),
            UiaPatternId.SelectionItem => HasAction(node, AccessibleActions.Select),
            UiaPatternId.Text => node.Text is not null,
            UiaPatternId.Value => node.Text is not null && node.Role == AccessibleRole.Edit,
            _ => false,
        };

    /// <summary>Finds the number of an action to pass to the tree.</summary>
    /// <param name="node">The element.</param>
    /// <param name="action">The action's name.</param>
    /// <returns>The index in the element's actions, or -1 when it has no such action.</returns>
    internal static int IndexOf(AccessibleNode node, string action)
    {
        for (var index = 0; index < node.Actions.Count; index++)
        {
            if (string.Equals(node.Actions[index], action, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Checks whether an element lists an action.</summary>
    /// <param name="node">The element.</param>
    /// <param name="action">The action's name.</param>
    /// <returns>Whether it does.</returns>
    private static bool HasAction(AccessibleNode node, string action) => IndexOf(node, action) >= 0;
}
