// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Platform;

/// <summary>A recently opened document.</summary>
/// <param name="FilePath">The file path.</param>
/// <param name="Visited">When the document was last opened.</param>
[DebuggerDisplay("RecentDocument: {FilePath}")]
public sealed record RecentDocument(string FilePath, DateTimeOffset Visited)
{
    /// <summary>Gets the file name.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Gets the containing folder.</summary>
    public string Folder => Path.GetDirectoryName(FilePath) ?? string.Empty;
}
