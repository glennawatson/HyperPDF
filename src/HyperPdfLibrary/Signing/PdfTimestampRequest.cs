// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdf.Cms;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// An RFC 3161 timestamp request for a hash, with the check of the authority's reply. A client sends <see cref="Encode"/> to the
/// authority (an HTTP POST with the content type <see cref="RequestMediaType"/>) and passes the reply to <see cref="ReadResponse"/>.
/// </summary>
[DebuggerDisplay("PdfTimestampRequest")]
public sealed class PdfTimestampRequest
{
    /// <summary>The length of the random nonce that ties a reply to its request.</summary>
    private const int NonceLength = 8;

    /// <summary>The mask that keeps the nonce positive.</summary>
    private const byte PositiveMask = 0x7F;

    /// <summary>The request.</summary>
    private readonly Rfc3161TimestampRequest _request;

    /// <summary>Initializes a new instance of the <see cref="PdfTimestampRequest"/> class.</summary>
    /// <param name="request">The request.</param>
    private PdfTimestampRequest(Rfc3161TimestampRequest request) => _request = request;

    /// <summary>Gets the media type of a request.</summary>
    public static string RequestMediaType => "application/timestamp-query";

    /// <summary>Gets the media type of a reply.</summary>
    public static string ResponseMediaType => "application/timestamp-reply";

    /// <summary>Creates a request that asks the authority to include its certificate in the token.</summary>
    /// <param name="hash">The hash to stamp.</param>
    /// <param name="digest">The digest that made the hash.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentException">The digest cannot be named in a request, or the hash has the wrong length for it.</exception>
    public static PdfTimestampRequest Create(ReadOnlyMemory<byte> hash, PdfDigestMethod digest)
    {
        var cms = PdfSigningDigests.ToCms(digest);
        var length = CmsDigests.Length(cms);
        if (length == 0 || hash.Length != length)
        {
            throw new ArgumentException($"The hash is not a {PdfSigningDigests.DisplayName(digest)} hash.", nameof(hash));
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        nonce[0] &= PositiveMask;
        return new(Rfc3161TimestampRequest.CreateFromHash(hash, new Oid(CmsDigests.Oid(cms)), null, nonce, true));
    }

    /// <summary>Encodes the request.</summary>
    /// <returns>The DER-encoded TimeStampReq.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Encode() => _request.Encode();

    /// <summary>Checks an authority's reply against the request and takes the token out of it.</summary>
    /// <param name="response">The DER-encoded TimeStampResp.</param>
    /// <returns>The DER-encoded token to store in the signature or the document timestamp.</returns>
    /// <exception cref="CryptographicException">The authority refused the request, or its reply does not match it (a different hash, nonce or digest).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadResponse(ReadOnlyMemory<byte> response) => _request.ProcessResponse(response, out _).AsSignedCms().Encode();
}
