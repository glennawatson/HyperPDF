// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>A portfolio (ISO 32000-2 12.3.5): how the attached files are listed, sorted and first shown.</summary>
/// <param name="Schema">The columns, one per field. Their <see cref="PdfPortfolioField.Key"/> names the field in each file's collection item.</param>
[DebuggerDisplay("PdfPortfolioSpec: {Schema.Count} fields, view {View}")]
public sealed record PdfPortfolioSpec(IReadOnlyList<PdfPortfolioField> Schema)
{
    /// <summary>Gets the sort keys, most significant first; each must be a key in <see cref="Schema"/>.</summary>
    public IReadOnlyList<PdfPortfolioSort> Sort { get; init; } = [];

    /// <summary>Gets the name, in the embedded files name tree, of the file shown first; <see langword="null"/> for none.</summary>
    public string? InitialDocument { get; init; }

    /// <summary>Gets the initial view: <c>D</c> (details), <c>T</c> (tiles) or <c>H</c> (hidden, so the file shows as an ordinary document).</summary>
    public string View { get; init; } = "D";
}
