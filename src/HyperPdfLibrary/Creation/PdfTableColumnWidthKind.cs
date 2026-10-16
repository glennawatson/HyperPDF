// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>Specifies how a <see cref="PdfFlowTable"/> column's width is calculated.</summary>
public enum PdfTableColumnWidthKind
{
    /// <summary>
    /// The column is sized from its unwrapped content width and participates in sharing available space when
    /// <see cref="PdfFlowTable.FillAvailableWidth"/> is enabled or when the table exceeds the page width.
    /// </summary>
    Auto = 0,

    /// <summary>The column has an explicit width in PDF points.</summary>
    Fixed = 1,

    /// <summary>
    /// The column receives a weighted share of the remaining available content width after fixed and automatic columns
    /// are measured.
    /// </summary>
    Proportional = 2,
}
