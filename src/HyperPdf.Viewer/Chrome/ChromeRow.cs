// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// A row of controls laid out left to right, with the place the next one goes. Each control added makes the next row, so a row is a value
/// that is carried from one control to the next. A control that would pass <see cref="Limit"/> starts a new line instead.
/// </summary>
/// <param name="Items">Receives each control.</param>
/// <param name="Runs">The text measurer.</param>
/// <param name="Metrics">The sizes.</param>
/// <param name="Height">The height of each control.</param>
/// <param name="Start">The left edge a new line starts at.</param>
/// <param name="Next">Where the next control goes across.</param>
/// <param name="Top">Where the next control goes down.</param>
/// <param name="Lines">The lines the row has taken.</param>
[DebuggerDisplay("ChromeRow: next at {Next}")]
internal sealed record ChromeRow(List<ChromeItem> Items, TextRuns Runs, ChromeMetrics Metrics, int Height, int Start, int Next, int Top, int Lines)
{
    /// <summary>Initializes a new instance of the <see cref="ChromeRow"/> class at a place.</summary>
    /// <param name="items">Receives each control.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="x">Where the first control goes across.</param>
    /// <param name="y">Where the first control goes down.</param>
    /// <param name="height">The height of each control.</param>
    internal ChromeRow(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, int x, int y, int height)
        : this(items, runs, metrics, height, x, x, y, 1)
    {
    }

    /// <summary>Gets how far across a control may reach before a new line starts.</summary>
    internal int Limit { get; init; } = int.MaxValue;

    /// <summary>Gets a value indicating whether a button with an icon shows its text beside it.</summary>
    internal bool ShowLabels { get; init; } = true;

    /// <summary>Adds a button that runs a command.</summary>
    /// <param name="text">The text.</param>
    /// <param name="command">The command a press runs.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the button in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Button(string text, ICommand? command, bool enabled) => Add(new(ChromeItemKind.Button, command, 0, text, default, enabled, false));

    /// <summary>Adds a button that runs a command, with an icon before its text.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="text">The text, which is also the name a screen reader says.</param>
    /// <param name="command">The command a press runs.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the button in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Button(ChromeIcon icon, string text, ICommand? command, bool enabled) =>
        Add(new(ChromeItemKind.Button, command, 0, text, default, enabled, false) { Icon = icon });

    /// <summary>Adds a button of a dialog, whose press is an input to that dialog.</summary>
    /// <param name="text">The text.</param>
    /// <param name="action">What a press means to the dialog.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the button in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Button(string text, OverlayAction action, bool enabled) => Add(new(ChromeItemKind.Button, action, 0, text, default, enabled, false));

    /// <summary>Adds a button that shows its icon alone, whatever the setting for names beside icons. What it does is plain from the text next to it.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="text">The name a screen reader says.</param>
    /// <param name="command">The command a press runs.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the button in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow IconButton(ChromeIcon icon, string text, ICommand? command, bool enabled) =>
        Add(new(ChromeItemKind.Button, command, 0, text, default, enabled, false) { Icon = icon, HidesText = true });

    /// <summary>Adds a control the caller made, such as a button of a group that passes its own value to a shared command. Its place is set here.</summary>
    /// <param name="item">The control, without its place.</param>
    /// <returns>The row with the control in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Control(ChromeItem item) => Add(item);

    /// <summary>Adds plain text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The row with the text in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Label(string text) => Add(new(ChromeItemKind.Label, null, 0, text, default, true, false));

    /// <summary>Leaves a space.</summary>
    /// <returns>The row after the space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Space() => this with { Next = Next + Metrics.Padding };

    /// <summary>Adds an upright line between two groups of controls.</summary>
    /// <returns>The row after the line.</returns>
    internal ChromeRow Divider()
    {
        Items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(Next, Top, Metrics.Padding, Height), true, false));
        return this with { Next = Next + Metrics.Padding + Metrics.Gap };
    }

    /// <summary>Measures a control: its icon, its text and the space each side of them.</summary>
    /// <param name="item">The control.</param>
    /// <param name="hidesText">Whether only its icon shows.</param>
    /// <returns>The width in device pixels.</returns>
    private int WidthOf(ChromeItem item, bool hidesText)
    {
        var textWidth = hidesText ? 0 : Runs.Measure(item.Text, ChromeBars.Style(Metrics));
        if (item.Icon == ChromeIcon.None)
        {
            return textWidth + Metrics.Padding + Metrics.Padding;
        }

        // A button with an icon stands closer to its neighbours, as the icon already sets it apart.
        var padding = IconPadding(hidesText);
        return Metrics.IconSize + (textWidth > 0 ? ChromePainter.IconGap(Metrics) : 0) + textWidth + padding + padding;
    }

    /// <summary>Gets the space each side of a button with an icon: a little less when the icon stands alone, so a row of them stays compact.</summary>
    /// <param name="iconAlone">Whether the button shows no text.</param>
    /// <returns>The space in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IconPadding(bool iconAlone) => iconAlone ? Metrics.Gap + (Metrics.Gap >> 1) : Metrics.Gap + Metrics.Gap;

    /// <summary>Adds a control, on a new line when it would pass the limit.</summary>
    /// <param name="item">The control, without its place.</param>
    /// <returns>The row with the control in it.</returns>
    private ChromeRow Add(ChromeItem item)
    {
        var hidesText = item.Icon != ChromeIcon.None && (item.HidesText || !ShowLabels);
        var width = WidthOf(item, hidesText);
        var wraps = Next + width > Limit && Next > Start;

        // A line is as tall as a bar: the control's height and the gap above and below it.
        var x = wraps ? Start : Next;
        var y = wraps ? Top + Height + Metrics.Gap + Metrics.Gap : Top;
        Items.Add(item with { Bounds = new(x, y, width, Height), HidesText = hidesText });
        return this with { Next = x + width + Metrics.Gap, Top = y, Lines = wraps ? Lines + 1 : Lines };
    }
}
