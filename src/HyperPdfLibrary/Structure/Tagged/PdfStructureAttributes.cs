// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// The attributes of a structure element that matter for reading, from its <c>/A</c> attribute objects and its
/// <c>/C</c> classes (the element's own attributes win).
/// </summary>
/// <param name="Placement">The Layout <c>/Placement</c>, such as Block or Inline; empty when not given.</param>
/// <param name="BoundingBox">The Layout <c>/BBox</c> in user space, or <see langword="null"/>.</param>
/// <param name="Scope">The Table <c>/Scope</c> of a header cell.</param>
/// <param name="Headers">The Table <c>/Headers</c>: the element ids of the header cells that label this cell.</param>
/// <param name="RowSpan">The Table <c>/RowSpan</c>, at least 1.</param>
/// <param name="ColumnSpan">The Table <c>/ColSpan</c>, at least 1.</param>
/// <param name="ListNumbering">The List <c>/ListNumbering</c>, such as Decimal or Disc; empty when not given.</param>
/// <param name="Summary">The Table <c>/Summary</c>; empty when not given.</param>
[DebuggerDisplay("PdfStructureAttributes: {Placement} scope {Scope} span {RowSpan}x{ColumnSpan}")]
public sealed record PdfStructureAttributes(
    string Placement,
    PdfRectangle? BoundingBox,
    PdfTableScope Scope,
    string[] Headers,
    int RowSpan,
    int ColumnSpan,
    string ListNumbering,
    string Summary)
{
    /// <summary>Gets the attributes of an element that gives none.</summary>
    public static PdfStructureAttributes None { get; } = new(string.Empty, null, PdfTableScope.None, [], 1, 1, string.Empty, string.Empty);

    /// <summary>Gets the non-inherited list continuation flag, or null when no valid boolean is specified.</summary>
    public bool? ContinuedList { get; init; }

    /// <summary>Gets the non-inherited PrintField role, checked state and alternate name, retaining omission separately from an explicit value.</summary>
    public PdfPrintFieldAttributes PrintField { get; init; }

    /// <summary>Gets whether a previous-list byte-string ID is specified, including an empty string.</summary>
    public bool HasContinuedFrom => ContinuedFromValue.Kind == PdfKind.String;

    /// <summary>Gets the previous list's exact byte-string ID without decoding it as text; empty when absent.</summary>
    public ReadOnlySpan<byte> ContinuedFromId => ContinuedFromValue.AsStringBytes();

    /// <summary>Gets the retained source string without copying its backing bytes.</summary>
    internal PdfValue ContinuedFromValue { get; init; }
}
