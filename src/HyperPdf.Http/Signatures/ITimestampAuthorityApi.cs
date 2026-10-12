// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Http.Signatures;

/// <summary>An RFC 3161 timestamp authority's HTTP endpoint.</summary>
[Headers("User-Agent: HyperPDF", "Accept: application/timestamp-reply")]
public interface ITimestampAuthorityApi
{
    /// <summary>Sends a timestamp query.</summary>
    /// <param name="path">The path and query relative to the base address.</param>
    /// <param name="query">The DER query, sent as <c>application/timestamp-query</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response; the caller disposes it.</returns>
    [Post("/{**path}")]
    Task<HttpResponseMessage> StampAsync(string path, [Body] HttpContent query, CancellationToken cancellationToken);
}
