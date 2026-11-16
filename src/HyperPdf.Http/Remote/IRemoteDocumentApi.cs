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
    /// <returns>A cold signal giving the response once, which the subscriber disposes; unsubscribing cancels the request.</returns>
    [Get("/{**path}")]
    IObservable<HttpResponseMessage> Download(string path);
}
