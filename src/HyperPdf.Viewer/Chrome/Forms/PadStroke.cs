// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>A stroke drawn on a dialog's drawing pad: the points so far, and whether the pointer was let go.</summary>
/// <param name="Points">The points of the stroke, in device pixels from the top left corner of the pad.</param>
/// <param name="Done">Whether the stroke is finished.</param>
[DebuggerDisplay("PadStroke: {Points.Length} points, done {Done}")]
internal readonly record struct PadStroke(PagePoint[] Points, bool Done);
