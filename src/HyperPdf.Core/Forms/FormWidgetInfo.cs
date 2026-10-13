// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms;

/// <summary>One widget of a field, as the form design shows it.</summary>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Index">The widget's annotation index on the page, as <see cref="FormField.Index"/> reports it.</param>
/// <param name="Bounds">The rectangle in page space (points, top-left origin).</param>
/// <param name="ExportValue">The export value of a check box or radio button; otherwise <see langword="null"/>.</param>
/// <param name="IsChecked">Whether a check box or radio button is on.</param>
[DebuggerDisplay("FormWidgetInfo: page {PageIndex} index {Index}")]
public sealed record FormWidgetInfo(int PageIndex, int Index, PageRect Bounds, string? ExportValue, bool IsChecked);
