// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Finding;

/// <summary>The counts of one folder search, which change as each file finishes.</summary>
internal sealed class FolderSearchProgress
{
    /// <summary>Gets or sets the number of files to search.</summary>
    internal int Total { get; set; }

    /// <summary>Gets or sets the number of files searched so far.</summary>
    internal int Searched { get; set; }

    /// <summary>Gets or sets the number of matches found so far.</summary>
    internal int Matches { get; set; }

    /// <summary>Gets or sets the number of files with at least one match.</summary>
    internal int WithMatches { get; set; }
}
