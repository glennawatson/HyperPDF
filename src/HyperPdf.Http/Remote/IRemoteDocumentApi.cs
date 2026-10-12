// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Http.Remote;

/// <summary>Downloads documents over HTTP(S).</summary>
[Headers("User-Agent: HyperPDF")]
public interface IRemoteDocumentApi
{
    /// <summary>Downloads a document.</summary>
    /// <param name="path">The path and query relative to the base address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response; the caller disposes it.</returns>
    [Get("/{**path}")]
    Task<HttpResponseMessage> DownloadAsync(string path, CancellationToken cancellationToken);
}
