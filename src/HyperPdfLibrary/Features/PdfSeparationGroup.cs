// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The separation pages that together make one printed page of a preseparated document.</summary>
/// <param name="Index">The zero based position of the group among the document's groups, in order of each group's first page.</param>
/// <param name="Separations">The separation pages in the order the <c>/Pages</c> array lists them.</param>
[DebuggerDisplay("PdfSeparationGroup: {Index} with {Separations.Length} separations")]
public sealed record PdfSeparationGroup(int Index, PdfSeparationPage[] Separations);
