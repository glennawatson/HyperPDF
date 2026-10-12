// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Search;

/// <summary>What a folder search found in one file.</summary>
/// <param name="Path">The file.</param>
/// <param name="Matches">The matches, in page order.</param>
/// <param name="Problem">Why the file could not be searched, such as needing a password; otherwise <see langword="null"/>.</param>
/// <param name="IsTruncated">Whether the file had more matches than were kept.</param>
[DebuggerDisplay("FolderSearchFile: {Path}: {Matches.Count} matches")]
public sealed record FolderSearchFile(string Path, IReadOnlyList<FolderSearchMatch> Matches, string? Problem, bool IsTruncated);
