// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What was asked of the renderer for the pictures of the pages panel, for the tab shown.</summary>
/// <param name="Tab">The tab the asks are for.</param>
/// <param name="Asked">What was asked for each page, by its zero based number.</param>
[DebuggerDisplay("ThumbnailAsks: {Asked.Count} asked")]
internal sealed record ThumbnailAsks(DocumentTabViewModel? Tab, Dictionary<int, ThumbnailAsk> Asked)
{
    /// <summary>Gets the state before anything was asked.</summary>
    internal static ThumbnailAsks None => new(null, []);
}
