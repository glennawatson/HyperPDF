// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Gets RFC 3161 timestamp tokens from a timestamp authority, so it can be proved later when a signature or a
/// document existed: a signature's value is stamped for a signature timestamp, the signed bytes for a document one.
/// </summary>
public interface ISignatureTimestamper
{
    /// <summary>Gets the usual size of a token in bytes, which sizes the room reserved for it in the file, or zero when it is not known.</summary>
    int EstimatedTokenSize { get; }

    /// <summary>Gets a timestamp token over a hash. Only the hash is sent to the authority, never the document.</summary>
    /// <param name="hash">The hash to stamp.</param>
    /// <param name="digest">The digest that made the hash.</param>
    /// <param name="cancellationToken">Stops waiting for the authority.</param>
    /// <returns>The DER-encoded timestamp token.</returns>
    ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, SigningDigest digest, CancellationToken cancellationToken);
}
