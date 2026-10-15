// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>The table attributes of an inferred header or data cell (ISO 32000-2 14.8.5.7).</summary>
/// <param name="Scope">The cells a header cell labels; none for a data cell.</param>
/// <param name="RowSpan">The rows the cell covers.</param>
/// <param name="ColumnSpan">The columns the cell covers.</param>
[DebuggerDisplay("TagCell: {Scope} {RowSpan}x{ColumnSpan}")]
internal readonly record struct TagCell(PdfTableScope Scope, int RowSpan, int ColumnSpan);
