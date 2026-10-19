// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>A folder in a portfolio's folder tree.</summary>
/// <param name="Id">The folder id that file specifications use to name their folder.</param>
/// <param name="Name">The folder name.</param>
/// <param name="Description">The description, or null.</param>
/// <param name="Created">The creation date, or null.</param>
/// <param name="Modified">The modification date, or null.</param>
/// <param name="FreeRanges">The id ranges kept free (<c>/Free</c>).</param>
/// <param name="Children">The sub-folders, in <c>/Child</c> then <c>/Next</c> order.</param>
[DebuggerDisplay("PdfPortfolioFolder: {Id} {Name}")]
public sealed record PdfPortfolioFolder(
    int Id,
    string? Name,
    string? Description,
    DateTimeOffset? Created,
    DateTimeOffset? Modified,
    PdfFolderIdRange[] FreeRanges,
    PdfPortfolioFolder[] Children);
