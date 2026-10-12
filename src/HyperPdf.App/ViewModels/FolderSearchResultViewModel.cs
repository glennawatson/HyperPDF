// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

namespace HyperPdf.App.ViewModels;

/// <summary>One line of folder search results: a match in a file, or a file that could not be searched.</summary>
/// <param name="Path">The file.</param>
/// <param name="PageIndex">The page of the match, zero-based; -1 for a file that could not be searched.</param>
/// <param name="Snippet">The words around the match, or why the file could not be searched.</param>
[DebuggerDisplay("FolderSearchResultViewModel: {FileName} {PageIndex}: {Snippet}")]
public sealed record FolderSearchResultViewModel(string Path, int PageIndex, string Snippet)
{
    /// <summary>Gets the file's name without its folder.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Gets a value indicating whether this line is a match that can be opened.</summary>
    public bool IsMatch => PageIndex >= 0;

    /// <summary>Gets where the match is, such as "report.pdf, page 4".</summary>
    public string Location => IsMatch ? string.Create(CultureInfo.CurrentCulture, $"{FileName}, page {PageIndex + 1}") : FileName;

    /// <summary>Gets the line as a screen reader says it.</summary>
    public string SpokenText => $"{Location}: {Snippet}";
}
