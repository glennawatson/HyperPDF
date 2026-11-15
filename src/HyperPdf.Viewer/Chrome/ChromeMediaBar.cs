// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The bar under the toolbar that plays sound and film: what is playing, how far it is, and the buttons that move it.</summary>
internal static class ChromeMediaBar
{
    /// <summary>Tells whether the bar is shown: the player was made and has something loaded in it.</summary>
    /// <param name="media">The sound and film of the tab shown.</param>
    /// <returns><see langword="true"/> when the bar is shown.</returns>
    internal static bool IsShown(DocumentMediaViewModel media) => media.HasPlayer && media.Player.State != MediaPlayerState.Idle;

    /// <summary>Lays the bar out across the window.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="player">The player.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">Where the bar starts down the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <returns>The height the bar takes.</returns>
    internal static int Add(List<ChromeItem> items, MediaPlayerViewModel player, ChromeLook look, int top, int width)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, default, true, false));

        // The buttons, the clock and the name share one row, so they stand closer together than the main toolbar's buttons.
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, tight.Gap, top + tight.Gap, tight.ToolbarHeight - tight.Gap - tight.Gap) { Limit = width - tight.Gap };
        row = row.Button(UiText.Get("Play"), ChromeCommand.MediaPlay, player.CanPlay);
        row = row.Button(UiText.Get("Pause"), ChromeCommand.MediaPause, player.CanPause);
        row = row.Button(UiText.Get("Stop"), ChromeCommand.MediaStop, player.CanMove);
        row = row.Button(UiText.Get("Back ten seconds"), ChromeCommand.MediaBack, player.CanMove);
        row = row.Button(UiText.Get("Forward ten seconds"), ChromeCommand.MediaForward, player.CanMove);
        if (player.HasPicture)
        {
            row = row.Button(UiText.Get("Show the picture"), ChromeCommand.MediaPicture, true);
        }

        row = row.Space();
        row = row.Label(player.Clock);
        row = row.Label(player.Caption.Length > 0 ? player.Caption : player.Status);
        var height = row.Lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }
}
