// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What has been drawn on a dialog's drawing pad: the finished strokes, and the stroke being drawn.</summary>
/// <param name="Done">The finished strokes, in device pixels from the top left corner of the pad.</param>
/// <param name="Live">The stroke being drawn, or an empty array when none is.</param>
[DebuggerDisplay("PadInk: {Done.Length} strokes, {Live.Length} points live")]
internal sealed record PadInk(PagePoint[][] Done, PagePoint[] Live)
{
    /// <summary>Gets the pad with nothing drawn on it.</summary>
    internal static PadInk Empty { get; } = new([], []);

    /// <summary>Gets a value indicating whether anything is drawn.</summary>
    internal bool IsBlank => Done.Length == 0 && Live.Length == 0;
}
