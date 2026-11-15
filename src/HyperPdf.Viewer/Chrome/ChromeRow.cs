// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>Adds a button.</summary>
    /// <param name="text">The text.</param>
    /// <param name="command">What the button does.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the button in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Button(string text, ChromeCommand command, bool enabled) => Add(ChromeItemKind.Button, text, command, enabled);

    /// <summary>Adds a button that is one of a group, which shows whether it is the chosen one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="command">What the button does.</param>
    /// <param name="index">Which member of the group it stands for.</param>
    /// <param name="selected">Whether it is the chosen one.</param>
    /// <returns>The row with the button in it.</returns>
    internal ChromeRow Button(string text, ChromeCommand command, int index, bool selected)
    {
        var next = Add(ChromeItemKind.Button, text, command, true);
        Items[^1] = Items[^1] with { Index = index, Selected = selected };
        return next;
    }

    /// <summary>Adds plain text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The row with the text in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Label(string text) => Add(ChromeItemKind.Label, text, ChromeCommand.None, true);

    /// <summary>Leaves a space.</summary>
    /// <returns>The row after the space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChromeRow Space() => this with { Next = Next + Metrics.Padding };

    /// <summary>Adds a control, on a new line when it would pass the limit.</summary>
    /// <param name="kind">What kind of control.</param>
    /// <param name="text">The text.</param>
    /// <param name="command">What the control does.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    /// <returns>The row with the control in it.</returns>
    private ChromeRow Add(ChromeItemKind kind, string text, ChromeCommand command, bool enabled)
    {
        var width = Runs.Measure(text, ChromeBars.Style(Metrics)) + Metrics.Padding + Metrics.Padding;
        var wraps = Next + width > Limit && Next > Start;

        // A line is as tall as a bar: the control's height and the gap above and below it.
        var x = wraps ? Start : Next;
        var y = wraps ? Top + Height + Metrics.Gap + Metrics.Gap : Top;
        Items.Add(new(kind, command, 0, text, new(x, y, width, Height), enabled, false));
        return this with { Next = x + width + Metrics.Gap, Top = y, Lines = wraps ? Lines + 1 : Lines };
    }
}
