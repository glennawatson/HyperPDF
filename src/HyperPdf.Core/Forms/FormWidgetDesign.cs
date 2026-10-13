// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms;

/// <summary>Where a new field is shown.</summary>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Bounds">The rectangle in page space (points, top-left origin).</param>
[DebuggerDisplay("FormWidgetDesign: page {PageIndex}")]
public sealed record FormWidgetDesign(int PageIndex, PageRect Bounds)
{
    /// <summary>Gets the export value of this button in a radio group; null picks one.</summary>
    public string? ExportValue { get; init; }
}
