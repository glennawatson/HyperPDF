// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The result of reading a document's structure tree once, kept in the document's state.</summary>
/// <param name="Tree">The tree, or <see langword="null"/> when the document has none.</param>
internal sealed record StructureTreeRead(PdfStructureTree? Tree);
