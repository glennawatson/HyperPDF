// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>A tab remembered between runs.</summary>
[DebuggerDisplay("SessionTab: {FilePath} p{PageIndex}")]
public sealed record SessionTab
{
    /// <summary>Gets the file path.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Gets the page that was showing.</summary>
    public int PageIndex { get; init; }

    /// <summary>Gets a value indicating whether this was the selected tab.</summary>
    public bool IsSelected { get; init; }
}
