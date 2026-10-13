// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>One widget of a field, as the form design reads it.</summary>
/// <param name="Location">The widget's page and index in the page's annotations; <c>-1</c> for both when no page lists it.</param>
/// <param name="Bounds">The widget's rectangle in user space.</param>
/// <param name="ExportValue">The export value of a check box or radio button; <see langword="null"/> for other fields.</param>
/// <param name="IsChecked">Whether a check box or radio button is on.</param>
[DebuggerDisplay("PdfWidgetDetails: page {Location.PageIndex} {ExportValue}")]
public sealed record PdfWidgetDetails(PdfWidgetLocation Location, PdfRectangle Bounds, string? ExportValue, bool IsChecked);
