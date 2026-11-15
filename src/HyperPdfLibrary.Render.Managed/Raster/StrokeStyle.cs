// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>How a line is stroked, in the units of the path (ISO 32000-2, 8.4.3).</summary>
/// <param name="Width">The line width. Zero asks for the thinnest line the device shows.</param>
/// <param name="Cap">The shape of open ends: 0 butt, 1 round, 2 projecting square.</param>
/// <param name="Join">The shape of corners: 0 miter, 1 round, 2 bevel.</param>
/// <param name="MiterLimit">The longest a miter may be, as a multiple of the width, before the corner is bevelled.</param>
/// <param name="Dash">The lengths of dashes and gaps in turn, or empty for a solid line.</param>
/// <param name="DashPhase">How far into the dash pattern the line starts.</param>
[DebuggerDisplay("StrokeStyle: width {Width}, cap {Cap}, join {Join}")]
internal readonly record struct StrokeStyle(float Width, int Cap, int Join, float MiterLimit, ReadOnlyMemory<float> Dash, float DashPhase);
