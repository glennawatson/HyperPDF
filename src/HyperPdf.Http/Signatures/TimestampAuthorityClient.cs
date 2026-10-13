// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    /// <summary>The usual size of a token with the authority's certificate chain, in bytes.</summary>
    private const int UsualTokenSize = 6_000;

    /// <summary>The length of the random nonce that ties a reply to its request.</summary>
    private const int NonceLength = 8;

    /// <summary>The mask that keeps the nonce positive.</summary>
    private const byte PositiveMask = 0x7F;

    /// <summary>The object identifier of the SHA-256 digest.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The object identifier of the SHA-384 digest.</summary>
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";

    /// <summary>The object identifier of the SHA-512 digest.</summary>
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

    /// <summary>The object identifier of the SHA3-256 digest.</summary>
    private const string Sha3x256Oid = "2.16.840.1.101.3.4.2.8";

    /// <summary>The object identifier of the SHA3-384 digest.</summary>
    private const string Sha3x384Oid = "2.16.840.1.101.3.4.2.9";

    /// <summary>The object identifier of the SHA3-512 digest.</summary>
    private const string Sha3x512Oid = "2.16.840.1.101.3.4.2.10";

    /// <summary>The object identifier of the SHAKE256 digest.</summary>
    private const string Shake256Oid = "2.16.840.1.101.3.4.2.12";

    /// <summary>How long the authority may take to answer.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>The clients shared by every instance, one for each authority.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<ITimestampAuthorityApi>> Clients = new(StringComparer.OrdinalIgnoreCase);

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

        // One HTTP client serves every request to an authority for the life of the process, as HttpClient is meant to be
        // used, so a client made for each signing does not leave a connection pool nothing disposes.
        _api = Clients.GetOrAdd(address.GetLeftPart(UriPartial.Authority), static authority => new(() => CreateApi(authority))).Value;
    }

    /// <inheritdoc/>
    public int EstimatedTokenSize => UsualTokenSize;

    /// <summary>Asks the authority for a token over a hash and checks the reply matches the request.</summary>
    /// <param name="hash">The hash to stamp.</param>
    /// <param name="digest">The digest that made the hash.</param>
    /// <param name="cancellationToken">Stops waiting for the authority.</param>
    /// <returns>The DER-encoded token.</returns>
    /// <exception cref="CryptographicException">The authority refused the request or answered with something other than a matching token.</exception>
    public async ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, SigningDigest digest, CancellationToken cancellationToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        nonce[0] &= PositiveMask;
        var request = Rfc3161TimestampRequest.CreateFromHash(hash, new Oid(OidOf(digest)), null, nonce, true);
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

    /// <summary>Makes the client of an authority.</summary>
    /// <param name="authority">The scheme and host.</param>
    /// <returns>The client.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ITimestampAuthorityApi CreateApi(string authority) =>
        RefitClients.CreateTimestampAuthorityApi(new() { BaseAddress = new(authority), Timeout = Timeout });

    /// <summary>Gets the object identifier a request names a digest by.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The identifier.</returns>
    private static string OidOf(SigningDigest digest) => digest switch
    {
        SigningDigest.Sha384 => Sha384Oid,
        SigningDigest.Sha512 => Sha512Oid,
        SigningDigest.Sha3_256 => Sha3x256Oid,
        SigningDigest.Sha3_384 => Sha3x384Oid,
        SigningDigest.Sha3_512 => Sha3x512Oid,
        SigningDigest.Shake256 => Shake256Oid,
        _ => Sha256Oid,
    };
}
