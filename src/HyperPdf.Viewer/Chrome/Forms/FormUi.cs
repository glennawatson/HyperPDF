// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What the user has done to a form window, apart from the values the view model holds: which list is open, where the keyboard is, and what is drawn.</summary>
/// <param name="Open">The row whose list is open, or -1.</param>
/// <param name="Focus">The row whose text box is typed into, or -1.</param>
/// <param name="First">The first line shown.</param>
/// <param name="Picture">The picture beside the rows as it was last laid out, or <see langword="null"/>.</param>
/// <param name="Pad">Where the picture or the drawing pad lay when the window was last laid out, in device pixels.</param>
/// <param name="Ink">What is drawn on the drawing pad.</param>
/// <param name="Drawing">Whether a stroke is being drawn.</param>
/// <param name="Stroke">The points of the stroke being drawn.</param>
/// <param name="Seeded">Whether the pad was filled with the drawing the view model remembered, which the first new stroke replaces.</param>
[DebuggerDisplay("FormUi: open {Open}, focus {Focus}, first {First}")]
internal sealed record FormUi(int Open, int Focus, int First, IRenderSurface? Picture, LayoutRect Pad, PadInk Ink, bool Drawing, PagePoint[] Stroke, bool Seeded)
{
    /// <summary>Gets the state of a window the user has not touched.</summary>
    internal static FormUi Start { get; } = new(-1, -1, 0, null, default, PadInk.Empty, false, [], false);

    /// <summary>Gets the most lines the dialog has shown since it opened. The dialog stays that tall, so its buttons do not move when rows go away.</summary>
    internal int Tall { get; init; }
}
