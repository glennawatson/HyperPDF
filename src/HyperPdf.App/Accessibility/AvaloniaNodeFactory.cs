// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>Makes the app's accessible elements from Avalonia automation peers.</summary>
internal static class AvaloniaNodeFactory
{
    /// <summary>The number of the application element.</summary>
    internal const int ApplicationId = 0;

    /// <summary>The name of the application element.</summary>
    private const string ApplicationName = "HyperPDF";

    /// <summary>Makes the application element, the root of the tree.</summary>
    /// <param name="windows">The numbers of the windows.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode CreateApplication(IReadOnlyList<int> windows) => new(
        ApplicationId,
        -1,
        AccessibleRole.Application,
        ApplicationName,
        string.Empty,
        AccessibleStates.Enabled | AccessibleStates.Showing,
        default,
        windows,
        0,
        0,
        0,
        string.Empty,
        [],
        null,
        0);

    /// <summary>Makes an element from a peer.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="parentId">The parent's number.</param>
    /// <param name="peer">The peer.</param>
    /// <param name="children">The numbers of the children.</param>
    /// <returns>The element.</returns>
    internal static AccessibleNode Create(int id, int parentId, AutomationPeer peer, IReadOnlyList<int> children)
    {
        var heading = peer.GetHeadingLevel();
        var role = AvaloniaRoleMap.ToRole(peer.GetAutomationControlType(), heading);
        var landmark = AvaloniaRoleMap.ToLandmarkName(peer.GetLandmarkType());
        var place = SetOf(peer);
        return new(
            id,
            parentId,
            landmark.Length > 0 ? AccessibleRole.Landmark : role,
            peer.GetName() ?? string.Empty,
            peer.GetHelpText() ?? string.Empty,
            PeerFacts.StatesOf(peer, role),
            BoundsOf(peer),
            children,
            heading,
            place.Position,
            place.Size,
            landmark,
            PeerFacts.ActionsOf(peer),
            PeerFacts.TextOf(peer, role),
            0);
    }

    /// <summary>Gets a peer's bounds in screen pixels.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The bounds.</returns>
    private static AccessibleBounds BoundsOf(AutomationPeer peer)
    {
        var local = peer.GetBoundingRectangle();
        var rect = peer.ToScreen(local) ?? local;
        return new((int)Math.Round(rect.X), (int)Math.Round(rect.Y), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
    }

    /// <summary>Gets a peer's place in a set, from the peer itself or from the automation properties set on its control.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The place and the size of the set, or zeros.</returns>
    private static SetPlace SetOf(AutomationPeer peer)
    {
        if (peer is ISetMember member)
        {
            return new(member.PositionInSet, member.SetSize);
        }

        if (peer is not ControlAutomationPeer { Owner: { } owner })
        {
            return default;
        }

        var position = AutomationProperties.GetPositionInSet(owner);
        return position > 0 ? new(position, Math.Max(AutomationProperties.GetSizeOfSet(owner), position)) : default;
    }
}
