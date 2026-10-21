// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>Words for the layers panel: the visible notes on a layer and how a screen reader says an entry.</summary>
internal static class LayerText
{
    /// <summary>The note on a locked layer.</summary>
    internal const string LockedNote = "Locked";

    /// <summary>The percentage points in a magnification of 1.</summary>
    private const double PercentPerUnit = 100;

    /// <summary>Writes the visible note for a layer: that it is locked, and when the file shows it automatically by zoom.</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The note; empty when there is nothing to say.</returns>
    internal static string Note(DocumentLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var zoom = layer.HasZoomLimit ? ZoomNote(layer.ZoomMin, layer.ZoomMax) : string.Empty;
        if (!layer.IsLocked)
        {
            return zoom;
        }

        return zoom.Length == 0 ? LockedNote : string.Create(CultureInfo.CurrentCulture, $"{LockedNote}. {zoom}");
    }

    /// <summary>Writes a zoom range in words.</summary>
    /// <param name="min">The magnification from which the layer is shown; 0 for none.</param>
    /// <param name="max">The magnification below which the layer is shown; infinity for none.</param>
    /// <returns>The words.</returns>
    internal static string ZoomNote(double min, double max)
    {
        var low = Percent(min);
        var high = Percent(max);
        if (min > 0 && !double.IsPositiveInfinity(max))
        {
            return string.Create(CultureInfo.CurrentCulture, $"Shown from {low} up to {high} zoom");
        }

        return min > 0
            ? string.Create(CultureInfo.CurrentCulture, $"Shown from {low} zoom")
            : string.Create(CultureInfo.CurrentCulture, $"Shown below {high} zoom");
    }

    /// <summary>Says an entry as a screen reader should: its name, then that it is a group, locked, or limited by zoom.</summary>
    /// <param name="name">The name or label text.</param>
    /// <param name="isLabel">Whether the entry is a label.</param>
    /// <param name="note">The visible note.</param>
    /// <param name="children">How many entries sit under it.</param>
    /// <returns>The spoken name.</returns>
    internal static string Speak(string name, bool isLabel, string note, int children)
    {
        var kind = isLabel ? " group" : string.Empty;
        var detail = note.Length > 0 ? string.Create(CultureInfo.CurrentCulture, $", {note}") : string.Empty;
        var count = children > 0 ? string.Create(CultureInfo.CurrentCulture, $", {children} sublayers") : string.Empty;
        return string.Create(CultureInfo.CurrentCulture, $"{name}{kind}{detail}{count}");
    }

    /// <summary>Formats a magnification as a percentage.</summary>
    /// <param name="magnification">The magnification, where 1 is 100 percent.</param>
    /// <returns>The percentage, such as "50%".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Percent(double magnification) =>
        string.Create(CultureInfo.CurrentCulture, $"{(magnification * PercentPerUnit):0.#}%");
}
