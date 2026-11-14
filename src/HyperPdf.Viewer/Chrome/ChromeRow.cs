// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Places controls one after another along a row, each as wide as its text needs.</summary>
/// <param name="items">Receives the controls.</param>
/// <param name="runs">The text measurer.</param>
/// <param name="metrics">The sizes.</param>
/// <param name="x">Where the row starts.</param>
/// <param name="y">The top of the row.</param>
/// <param name="height">The height of the row.</param>
[DebuggerDisplay("ChromeRow: next at {_x}")]
internal sealed class ChromeRow(List<ChromeItem> items, TextRuns runs, ChromeMetrics metrics, int x, int y, int height)
{
    /// <summary>Where the next control goes.</summary>
    private int _x = x;

    /// <summary>Gets where the next control goes.</summary>
    internal int Next => _x;

    /// <summary>Adds a button.</summary>
    /// <param name="text">The text.</param>
    /// <param name="command">What the button does.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Button(string text, ChromeCommand command, bool enabled) => Add(ChromeItemKind.Button, text, command, enabled);

    /// <summary>Adds plain text.</summary>
    /// <param name="text">The text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Label(string text) => Add(ChromeItemKind.Label, text, ChromeCommand.None, true);

    /// <summary>Leaves a wider space between two groups of controls.</summary>
    internal void Space() => _x += metrics.Padding;

    /// <summary>Adds a control as wide as its text and its padding.</summary>
    /// <param name="kind">How it is drawn.</param>
    /// <param name="text">The text.</param>
    /// <param name="command">What it does.</param>
    /// <param name="enabled">Whether it can be pressed.</param>
    private void Add(ChromeItemKind kind, string text, ChromeCommand command, bool enabled)
    {
        var width = runs.Measure(text, ChromeBars.Style(metrics)) + metrics.Padding + metrics.Padding;
        items.Add(new(kind, command, 0, text, new(_x, y, width, height), enabled, false));
        _x += width + metrics.Gap;
    }
}
