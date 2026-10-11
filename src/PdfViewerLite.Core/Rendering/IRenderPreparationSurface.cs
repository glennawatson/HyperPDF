// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Rendering;

/// <summary>
/// A tile whose document content is prepared on the render worker and replayed by its presentation layer.
/// The surface remains owned by the existing tile cache; it does not expose or retain CPU pixels.
/// </summary>
public interface IRenderPreparationSurface : IRenderSurface
{
    /// <summary>Prepares the requested content before the surface enters the tile cache.</summary>
    /// <param name="request">The request that produced this surface.</param>
    /// <param name="cancellationToken">Cancels preparation when the scheduler stops.</param>
    /// <returns>Whether preparation completed.</returns>
    bool Prepare(in RenderRequest request, CancellationToken cancellationToken);
}
