// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>What the PDF/A structure type rules resolve element types against.</summary>
/// <param name="Standard">The standard structure types of the part's PDF version.</param>
/// <param name="RoleMap">The structure tree root's role map.</param>
/// <param name="StopAtStandard">Whether a standard type stands even when the role map maps it; PDF/A-1 reads it so.</param>
internal sealed record PdfAStructureTree(HashSet<PdfName> Standard, Dictionary<PdfName, PdfName> RoleMap, bool StopAtStandard);
