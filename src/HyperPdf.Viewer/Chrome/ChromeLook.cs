// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the window's controls are drawn with.</summary>
/// <param name="Runs">The drawn text.</param>
/// <param name="Theme">The colours.</param>
/// <param name="Metrics">The sizes.</param>
/// <param name="Thumbnail">Gets the picture of the page a row of the pages panel shows, or <see langword="null"/> while it is being made.</param>
/// <param name="Ink">Gets what is drawn on the drawing pad of the open dialog, or <see langword="null"/> when it has no pad.</param>
[DebuggerDisplay("ChromeLook: text {Metrics.TextSize} px")]
internal sealed record ChromeLook(TextRuns Runs, ChromeTheme Theme, ChromeMetrics Metrics, Func<int, IRenderSurface?> Thumbnail, Func<PadInk?> Ink)
{
    /// <summary>Gets the drawn icons, or <see langword="null"/> to draw none.</summary>
    internal IconRuns? Icons { get; init; }

    /// <summary>Gets a value indicating whether tool bar buttons show their names beside their icons.</summary>
    internal bool ShowLabels { get; init; } = true;
}
