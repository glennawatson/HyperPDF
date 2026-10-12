// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Downloads the exact official pdf.js generic browser release.</summary>
internal interface IPdfJsDownloadApi
{
    /// <summary>Returns the pinned release archive response.</summary>
    /// <param name="cancellationToken">Cancels downloading.</param>
    /// <returns>The owned HTTP response.</returns>
    [Get("/mozilla/pdf.js/releases/download/v6.3.289/pdfjs-6.3.289-dist.zip")]
    Task<HttpResponseMessage> GetArchiveAsync(CancellationToken cancellationToken);
}
