// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Search;

namespace HyperPdf.App.ViewModels;

/// <summary>A search hit listed in the sidebar.</summary>
/// <param name="Index">The position of the hit among all hits.</param>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Hit">The hit.</param>
/// <param name="Context">The text around the hit.</param>
[DebuggerDisplay("SearchResultItemViewModel: {PageIndex}: {Context}")]
public sealed record SearchResultItemViewModel(int Index, int PageIndex, SearchHit Hit, string Context)
{
    /// <summary>Gets the one based page number.</summary>
    public int PageNumber => PageIndex + 1;
}
