// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Search;

/// <summary>Says whether a file's search left a match out because too many were found.</summary>
internal sealed class FolderSearchTally
{
    /// <summary>Gets or sets a value indicating whether a match was left out.</summary>
    internal bool Truncated { get; set; }
}
