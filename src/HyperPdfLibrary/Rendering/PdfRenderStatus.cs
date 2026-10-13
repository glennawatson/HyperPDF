// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The outcome of a progressive render step, for a page drawn in slices.</summary>
public enum PdfRenderStatus
{
    /// <summary>The page is fully recorded and the tile was drawn.</summary>
    Done = 0,

    /// <summary>The pause callback asked to stop; call again with the same request to continue.</summary>
    Paused = 1,

    /// <summary>The cancellation token was signalled; the partial recording was discarded.</summary>
    Cancelled = 2,

    /// <summary>The page does not exist, the target is invalid or the pixels could not be read.</summary>
    Failed = 3,
}
