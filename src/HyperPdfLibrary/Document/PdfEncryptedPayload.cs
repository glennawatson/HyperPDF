// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// The encrypted document inside an unencrypted wrapper (ISO 32000-2 7.6.7): an embedded file whose file specification has
/// <c>/AFRelationship /EncryptedPayload</c> and an /EP dictionary.
/// </summary>
/// <param name="FileName">The payload's file name.</param>
/// <param name="CryptographicFilter">The /EP /Subtype: the name of the cryptographic filter that protects the payload.</param>
/// <param name="Version">The /EP /Version of that filter, or <see langword="null"/>.</param>
/// <param name="Description">The file specification's /Desc, or <see langword="null"/>.</param>
/// <param name="Data">The embedded file stream, or <see langword="null"/> when missing.</param>
[DebuggerDisplay("PdfEncryptedPayload: {FileName} ({CryptographicFilter})")]
public sealed record PdfEncryptedPayload(string FileName, string CryptographicFilter, string? Version, string? Description, PdfStream? Data)
{
    /// <summary>Decodes the payload's bytes: the encrypted document.</summary>
    /// <returns>The bytes, or empty when there is no stream.</returns>
    public byte[] GetBytes() => Data?.DecodeToArray() ?? [];

    /// <summary>Decodes the payload after reading its encoded bytes with async I/O.</summary>
    /// <param name="cancellationToken">Cancels reading and decoding.</param>
    /// <returns>The encrypted document bytes, or empty when the payload has no stream.</returns>
    public async ValueTask<byte[]> GetBytesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Data is not { } stream)
        {
            return [];
        }

        var raw = await stream.CopyRawDataAsync(cancellationToken).ConfigureAwait(false);
        var buffered = new PdfStream(stream.Dictionary, raw, 0, raw.Length, stream.Id, stream.IsEncrypted);
        return buffered.DecodeToArray(cancellationToken);
    }
}
