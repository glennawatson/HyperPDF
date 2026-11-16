// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Http.Signatures;

/// <summary>The HTTP endpoints of a certificate authority that serve revocation data: OCSP responders, CRL distribution points and issuer certificates.</summary>
[Headers("User-Agent: HyperPDF")]
public interface IRevocationApi
{
    /// <summary>Sends an OCSP request (RFC 6960, A.1).</summary>
    /// <param name="path">The path and query relative to the base address.</param>
    /// <param name="request">The DER request, sent as <c>application/ocsp-request</c>.</param>
    /// <returns>A cold signal giving the response once; the caller disposes it, and unsubscribing cancels the request.</returns>
    [Post("/{**path}")]
    [Headers("Accept: application/ocsp-response")]
    IObservable<HttpResponseMessage> RequestOcsp(string path, [Body] HttpContent request);

    /// <summary>Fetches a CRL or an issuer certificate.</summary>
    /// <param name="path">The path and query relative to the base address.</param>
    /// <returns>A cold signal giving the response once; the caller disposes it, and unsubscribing cancels the request.</returns>
    [Get("/{**path}")]
    IObservable<HttpResponseMessage> Fetch(string path);
}
