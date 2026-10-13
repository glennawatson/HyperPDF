// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Where one widget of a new field goes.</summary>
/// <param name="PageIndex">The zero based index of the page that shows the widget.</param>
/// <param name="Bounds">The widget's rectangle in the page's user space.</param>
[DebuggerDisplay("PdfWidgetPlacement: page {PageIndex}")]
public sealed record PdfWidgetPlacement(int PageIndex, PdfRectangle Bounds)
{
    /// <summary>Gets the export value of this button in a radio group, such as <c>Red</c>; null picks <c>Choice1</c>, <c>Choice2</c> and so on.</summary>
    public string? ExportValue { get; init; }
}
