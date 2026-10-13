// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Gets RFC 3161 timestamp tokens from a timestamp authority, so a signature or a document can later be shown to have
/// existed at a time. Only a hash leaves the machine, never the document. <see cref="PdfTimestampRequest"/> builds the request and
/// checks the reply for a client that talks to an authority over HTTP.
/// </summary>
public interface IPdfTimestampClient
{
    /// <summary>
    /// Gets the usual size of a token from this authority in bytes, which sizes the room reserved for it in the file, or zero
    /// when it is not known (the library then reserves a generous default).
    /// </summary>
    int EstimatedTokenSize { get; }

    /// <summary>Gets a timestamp token over a hash.</summary>
    /// <param name="hash">The hash to stamp: of a signature's value for a signature timestamp, or of the signed bytes for a document timestamp.</param>
    /// <param name="digest">The digest that made the hash.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The DER-encoded token: a CMS ContentInfo holding the TimeStampToken (RFC 3161, 2.4.2).</returns>
    ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, PdfDigestMethod digest, CancellationToken cancellationToken);
}
