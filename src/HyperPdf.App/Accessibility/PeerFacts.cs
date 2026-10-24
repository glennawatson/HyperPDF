// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>Reads what a screen reader needs from an Avalonia automation peer: its states, its actions and its text.</summary>
internal static class PeerFacts
{
    /// <summary>The name of the action that clicks a button or opens an item.</summary>
    internal const string ClickAction = "click";

    /// <summary>The name of the action that switches a check box or toggle.</summary>
    internal const string ToggleAction = "toggle";

    /// <summary>The name of the action that opens an expandable element.</summary>
    internal const string ExpandAction = "expand";

    /// <summary>The name of the action that closes an expandable element.</summary>
    internal const string CollapseAction = "collapse";

    /// <summary>The name of the action that selects an item.</summary>
    internal const string SelectAction = "select";

    /// <summary>Reads the states of a peer.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="role">The role the peer was given.</param>
    /// <returns>The states.</returns>
    internal static AccessibleStates StatesOf(AutomationPeer peer, AccessibleRole role)
    {
        var states = AccessibleStates.None;
        states |= peer.IsEnabled() ? AccessibleStates.Enabled : AccessibleStates.None;
        states |= peer.IsOffscreen() ? AccessibleStates.None : AccessibleStates.Showing;
        states |= peer.IsKeyboardFocusable() ? AccessibleStates.Focusable : AccessibleStates.None;
        states |= peer.HasKeyboardFocus() ? AccessibleStates.Focused : AccessibleStates.None;
        states |= ToggleStatesOf(peer);
        states |= SelectionStatesOf(peer);
        states |= ExpandStatesOf(peer);
        states |= EditStatesOf(peer, role);
        return states;
    }

    /// <summary>Lists what a peer can do, in the order the screen reader's default action comes first.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The action names; the index of an action is the number passed to <see cref="Invoke"/>.</returns>
    internal static List<string> ActionsOf(AutomationPeer peer)
    {
        var actions = new List<string>(1);
        if (peer is IInvokeProvider)
        {
            actions.Add(ClickAction);
        }

        if (peer is IToggleProvider)
        {
            actions.Add(ToggleAction);
        }

        if (peer is IExpandCollapseProvider expander)
        {
            actions.Add(expander.ExpandCollapseState == ExpandCollapseState.Expanded ? CollapseAction : ExpandAction);
        }

        if (peer is ISelectionItemProvider)
        {
            actions.Add(SelectAction);
        }

        return actions;
    }

    /// <summary>Carries out an action of a peer.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="action">The action's name.</param>
    /// <returns>Whether the peer could carry it out.</returns>
    internal static bool Invoke(AutomationPeer peer, string action)
    {
        switch (action)
        {
            case ClickAction when peer is IInvokeProvider invoke:
            {
                invoke.Invoke();
                return true;
            }

            case ToggleAction when peer is IToggleProvider toggle:
            {
                toggle.Toggle();
                return true;
            }

            case ExpandAction when peer is IExpandCollapseProvider expand:
            {
                expand.Expand();
                return true;
            }

            case CollapseAction when peer is IExpandCollapseProvider collapse:
            {
                collapse.Collapse();
                return true;
            }

            case SelectAction when peer is ISelectionItemProvider select:
            {
                select.Select();
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Reads the text a screen reader may read by character, word and line.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="role">The role the peer was given.</param>
    /// <returns>The text, or <see langword="null"/> when the peer has none to read that way.</returns>
    internal static AccessibleText? TextOf(AutomationPeer peer, AccessibleRole role)
    {
        if (role is not (AccessibleRole.Document or AccessibleRole.Edit) || peer is not IValueProvider value)
        {
            return null;
        }

        var text = value.Value ?? string.Empty;
        return role == AccessibleRole.Edit ? new(text, 0, -1, -1) : AccessibleText.Plain(text);
    }

    /// <summary>Reads the checked state of a toggle.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The states that follow from the toggle.</returns>
    private static AccessibleStates ToggleStatesOf(AutomationPeer peer) => peer is IToggleProvider toggle
        ? toggle.ToggleState switch
        {
            ToggleState.On => AccessibleStates.Checked,
            ToggleState.Indeterminate => AccessibleStates.Indeterminate,
            _ => AccessibleStates.None,
        }
        : AccessibleStates.None;

    /// <summary>Reads whether a peer can be selected and is.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The states that follow from the selection.</returns>
    private static AccessibleStates SelectionStatesOf(AutomationPeer peer) => peer is ISelectionItemProvider item
        ? AccessibleStates.Selectable | (item.IsSelected ? AccessibleStates.Selected : AccessibleStates.None)
        : AccessibleStates.None;

    /// <summary>Reads whether a peer can open and is open.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The states that follow from the expander.</returns>
    private static AccessibleStates ExpandStatesOf(AutomationPeer peer) => peer is IExpandCollapseProvider expander
        ? AccessibleStates.Expandable | (expander.ExpandCollapseState == ExpandCollapseState.Expanded ? AccessibleStates.Expanded : AccessibleStates.None)
        : AccessibleStates.None;

    /// <summary>Reads whether text can be edited.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="role">The role the peer was given.</param>
    /// <returns>The states that follow from the value.</returns>
    private static AccessibleStates EditStatesOf(AutomationPeer peer, AccessibleRole role)
    {
        if (peer is not IValueProvider value || role != AccessibleRole.Edit)
        {
            return AccessibleStates.None;
        }

        return value.IsReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.Editable;
    }
}
