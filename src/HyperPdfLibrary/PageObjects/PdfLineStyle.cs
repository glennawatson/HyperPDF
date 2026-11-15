// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.PageObjects;

/// <summary>How a stroked line looks apart from its width and colour: the shape of its ends and corners and its dashes (ISO 32000-2, 8.4.3).</summary>
/// <param name="Cap">How the ends of the line and of each dash are drawn.</param>
/// <param name="Join">How corners are drawn.</param>
/// <param name="Dash">The lengths of the dashes and gaps, alternating, in user space units; empty for a solid line.</param>
/// <param name="Phase">How far into the dash pattern the line starts.</param>
[DebuggerDisplay("PdfLineStyle: {Cap}, {Join}, {Dash.Length} dash lengths")]
public sealed record PdfLineStyle(PdfLineCap Cap, PdfLineJoin Join, float[] Dash, float Phase)
{
    /// <summary>Gets a solid line with square ends and mitred corners, which is how a line is drawn when nothing else is set.</summary>
    public static PdfLineStyle Solid { get; } = new(PdfLineCap.Butt, PdfLineJoin.Miter, [], 0);
}
