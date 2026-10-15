// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A line found to be a running header, a running footer or a page number.</summary>
/// <param name="Line">The line.</param>
/// <param name="Kind">Which pagination artifact it is.</param>
[DebuggerDisplay("PaginationLine: {Kind}")]
internal readonly record struct PaginationLine(LayoutLine Line, PdfPaginationKind Kind);
