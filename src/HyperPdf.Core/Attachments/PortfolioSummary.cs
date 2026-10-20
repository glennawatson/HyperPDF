// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Attachments;

/// <summary>What a PDF portfolio (the catalog's <c>/Collection</c>) says about how its files are shown.</summary>
/// <param name="View">The initial view: <c>D</c> for details, <c>T</c> for tiles, or <c>H</c> for hidden.</param>
/// <param name="FileCount">The number of files the portfolio lists.</param>
/// <param name="InitialFile">The name of the file the portfolio shows first, or <see langword="null"/> when it names none.</param>
[DebuggerDisplay("PortfolioSummary: {FileCount} files, view {View}")]
public sealed record PortfolioSummary(string View, int FileCount, string? InitialFile)
{
    /// <summary>Gets a value indicating whether the portfolio asks to be shown as an ordinary document.</summary>
    public bool IsHidden => string.Equals(View, "H", StringComparison.OrdinalIgnoreCase);
}
