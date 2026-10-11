// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Rendering;

/// <summary>A queued render job.</summary>
/// <param name="Key">The cache key of the result.</param>
/// <param name="Document">The document to render.</param>
/// <param name="Info">What to render.</param>
/// <param name="Width">The output width in pixels.</param>
/// <param name="Height">The output height in pixels.</param>
/// <param name="Priority">The queue priority.</param>
/// <param name="Client">The requesting client.</param>
/// <param name="Generation">The client generation when requested.</param>
/// <param name="Tone">The page tone applied after rendering.</param>
[DebuggerDisplay("RenderRequest: {Key} {Priority}")]
public readonly record struct RenderRequest(
    TileKey Key,
    IDocument Document,
    PageRenderInfo Info,
    int Width,
    int Height,
    RenderPriority Priority,
    RenderClient Client,
    int Generation,
    PageTone Tone)
{
    /// <summary>Gets the active tab's cancellation token, when the request has one.</summary>
    public CancellationToken CancellationToken { get; init; }
}
