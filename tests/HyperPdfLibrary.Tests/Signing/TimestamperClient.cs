// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Lets the library use the test timestamp authority, which speaks the app's timestamp interface.</summary>
[DebuggerDisplay("TimestamperClient")]
internal sealed class TimestamperClient : IPdfTimestampClient
{
    /// <summary>The authority.</summary>
    private readonly ISignatureTimestamper _authority;

    /// <summary>Initializes a new instance of the <see cref="TimestamperClient"/> class.</summary>
    /// <param name="authority">The authority.</param>
    internal TimestamperClient(ISignatureTimestamper authority) => _authority = authority;

    /// <inheritdoc/>
    public int EstimatedTokenSize => _authority.EstimatedTokenSize;

    /// <inheritdoc/>
    public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, PdfDigestMethod digest, CancellationToken cancellationToken) =>
        _authority.TimestampAsync(hash, ToSigningDigest(digest), cancellationToken);

    /// <summary>Maps a digest to the app's.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The app's digest.</returns>
    internal static SigningDigest ToSigningDigest(PdfDigestMethod digest) => digest switch
    {
        PdfDigestMethod.Sha3_256 => SigningDigest.Sha3_256,
        PdfDigestMethod.Sha3_384 => SigningDigest.Sha3_384,
        PdfDigestMethod.Sha3_512 => SigningDigest.Sha3_512,
        PdfDigestMethod.Shake256 => SigningDigest.Shake256,
        PdfDigestMethod.Sha384 => SigningDigest.Sha384,
        PdfDigestMethod.Sha512 => SigningDigest.Sha512,
        _ => SigningDigest.Sha256,
    };

    /// <summary>Maps a digest of the app to the library's.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The library's digest.</returns>
    internal static PdfDigestMethod ToDigest(SigningDigest digest) => digest switch
    {
        SigningDigest.Sha3_256 => PdfDigestMethod.Sha3_256,
        SigningDigest.Sha3_384 => PdfDigestMethod.Sha3_384,
        SigningDigest.Sha3_512 => PdfDigestMethod.Sha3_512,
        SigningDigest.Shake256 => PdfDigestMethod.Shake256,
        SigningDigest.Sha384 => PdfDigestMethod.Sha384,
        SigningDigest.Sha512 => PdfDigestMethod.Sha512,
        _ => PdfDigestMethod.Sha256,
    };
}
