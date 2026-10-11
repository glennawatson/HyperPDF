// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Prepares page recordings on a worker and replays them into a graphics target on its permitted thread.</summary>
public interface IHyperPdfGpuRenderer
{
    /// <summary>Records the requested page after its external resources have been loaded.</summary>
    /// <param name="info">The page and rendering flags.</param>
    /// <param name="cancellationToken">Cancels recording.</param>
    /// <returns>Whether the page exists and was recorded.</returns>
    bool Prepare(in PageRenderInfo info, CancellationToken cancellationToken);

    /// <summary>Loads and records a page through async I/O.</summary>
    /// <param name="info">The page and rendering flags.</param>
    /// <param name="cancellationToken">Cancels loading and recording.</param>
    /// <returns>Whether the page exists and was recorded.</returns>
    ValueTask<bool> PrepareAsync(PageRenderInfo info, CancellationToken cancellationToken);

    /// <summary>Submits a page replay to the caller's graphics target.</summary>
    /// <param name="info">The page region and rendering flags.</param>
    /// <param name="target">The target kept alive through GPU completion and presentation.</param>
    /// <returns>Whether drawing was submitted.</returns>
    bool Render(in PageRenderInfo info, IPdfRenderTarget target);
}
