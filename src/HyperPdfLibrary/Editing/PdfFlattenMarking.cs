// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>How a flattened drawing is marked in the content of a tagged document.</summary>
/// <param name="Kind">Whether it is not marked, an artifact, or content of a structure element.</param>
/// <param name="Mcid">The marked content id of content, otherwise -1.</param>
/// <param name="Tag">The marked content tag of content: the structure type of the element that owns it.</param>
[DebuggerDisplay("PdfFlattenMarking: {Kind} {Mcid}")]
internal readonly record struct PdfFlattenMarking(PdfFlattenMarkingKind Kind, int Mcid, PdfName Tag)
{
    /// <summary>Gets the marking of a drawing in a document that is not tagged.</summary>
    internal static PdfFlattenMarking None { get; } = new(PdfFlattenMarkingKind.None, -1, default);

    /// <summary>Gets the marking of a drawing that is not part of the logical structure.</summary>
    internal static PdfFlattenMarking Artifact { get; } = new(PdfFlattenMarkingKind.Artifact, -1, default);
}
