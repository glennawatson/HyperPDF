// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>A table cell and where it starts on the table's grid.</summary>
/// <param name="Cell">The TH or TD element.</param>
/// <param name="Row">The first row it covers, from 0.</param>
/// <param name="Column">The first column it covers, from 0.</param>
internal readonly record struct TableCellPlace(PdfStructureElement Cell, int Row, int Column);
