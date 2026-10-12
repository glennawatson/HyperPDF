// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>The page a document showed when it was last closed.</summary>
[DebuggerDisplay("LastViewedPage: {FilePath} p{PageIndex}")]
public sealed record LastViewedPage
{
    /// <summary>Gets the file path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Gets the zero-based page that was showing.</summary>
    public int PageIndex { get; init; }
}
