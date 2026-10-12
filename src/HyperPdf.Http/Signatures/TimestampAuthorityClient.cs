// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.Http.Signatures;

/// <summary>
/// Gets timestamp tokens from an RFC 3161 timestamp authority over HTTP, such as a certificate authority's free
/// service, so signatures carry a trusted signing time. Only a hash of what is stamped is sent, never the document.
/// </summary>
[DebuggerDisplay("TimestampAuthorityClient: {_address}")]
public sealed class TimestampAuthorityClient : ISignatureTimestamper
{
    /// <summary>How long the authority may take to answer.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>The authority's address.</summary>
    private readonly Uri _address;

    /// <summary>The Refit client.</summary>
    private readonly ITimestampAuthorityApi _api;

    /// <summary>Initializes a new instance of the <see cref="TimestampAuthorityClient"/> class.</summary>
    /// <param name="address">The authority's address, for example <c>http://timestamp.digicert.com</c>.</param>
    public TimestampAuthorityClient(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _address = address;
        _api = RefitClients.CreateTimestampAuthorityApi(new() { BaseAddress = new(address.GetLeftPart(UriPartial.Authority)), Timeout = Timeout });
    }

    /// <summary>Asks the authority for a token over a hash of the data and checks the reply matches the request.</summary>
    /// <param name="data">The data to stamp.</param>
    /// <param name="cancellationToken">Stops waiting for the authority.</param>
    /// <returns>The DER-encoded token.</returns>
    /// <exception cref="CryptographicException">The authority refused the request or answered with something other than a matching token.</exception>
    public async ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var request = Rfc3161TimestampRequest.CreateFromData(data.Span, HashAlgorithmName.SHA256, requestSignerCertificates: true);
        using var content = new ByteArrayContent(request.Encode());
        content.Headers.ContentType = new("application/timestamp-query");
        using var response = await _api.StampAsync(_address.PathAndQuery.TrimStart('/'), content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new CryptographicException($"The timestamp server answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var reply = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return request.ProcessResponse(reply, out _).AsSignedCms().Encode();
    }
}
