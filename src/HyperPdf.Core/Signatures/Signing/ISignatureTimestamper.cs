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
    /// <summary>Gets a timestamp token over some data; the authority signs a SHA-256 hash of it with the time.</summary>
    /// <param name="data">The data to stamp.</param>
    /// <param name="cancellationToken">Stops waiting for the authority.</param>
    /// <returns>The DER-encoded timestamp token.</returns>
    ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
