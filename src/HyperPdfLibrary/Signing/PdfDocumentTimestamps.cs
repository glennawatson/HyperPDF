// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Adds document timestamps: an RFC 3161 token over the whole file as the /Contents of a /DocTimeStamp signature (ISO 32000-2, 12.8.5).</summary>
internal static class PdfDocumentTimestamps
{
    /// <summary>The prefix of generated field names.</summary>
    private const string NamePrefix = "DocTimeStamp";

    /// <summary>The bytes of a timestamp container beyond the token itself.</summary>
    private const int ContainerOverhead = 128;

    /// <summary>Adds a document timestamp.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the wait for the timestamp authority.</param>
    /// <returns>The timestamped file.</returns>
    /// <exception cref="ArgumentException">The options have no timestamp client.</exception>
    /// <exception cref="NotSupportedException">This computer cannot compute the timestamp digest.</exception>
    /// <exception cref="PdfException">The token does not fit even after the file is built again with more room.</exception>
    internal static async ValueTask<PdfSigningResult> AddAsync(PdfDocument document, PdfSigningOptions options, CancellationToken cancellationToken)
    {
        var client = options.Timestamp ?? throw new ArgumentException("A document timestamp needs a timestamp client.", nameof(options));
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        await PdfSigningEngine.PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        var plan = PlanFor(document, options);
        var withMac = PdfSigningMac.IsNeeded(document.Objects);
        var capacity = PdfContentsSizing.RoundUp(PdfContentsSizing.TimestampSize(client) + ContainerOverhead + options.Headroom + (withMac ? PdfSigningMac.AttachedSize() + ContainerOverhead : 0));
        var needed = 0;
        using var workspace = PdfSigningWorkspace.Open(document);
        for (var attempt = 1; attempt <= PdfSigningEngine.MaxAttempts; attempt++)
        {
            var prepared = PdfSigningEngine.Build(workspace, plan, false, options, [], capacity, cancellationToken);
            var token = await client.TimestampAsync(Hash(prepared, options.TimestampDigest), options.TimestampDigest, cancellationToken).ConfigureAwait(false);
            if (prepared.Mac is { } mac)
            {
                token = PdfSigningMac.Attach(mac, prepared.File, token);
            }

            if (PdfSigningEngine.TryFinish(prepared, token, attempt, out needed) is { } result)
            {
                return result;
            }

            prepared.Mac?.Wipe();
            capacity = PdfSigningEngine.Grow(needed, options.Headroom);
        }

        throw PdfSigningEngine.NoRoom(needed, capacity);
    }

    /// <summary>Hashes the signed bytes of the prepared file.</summary>
    /// <param name="prepared">The prepared file.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="NotSupportedException">This computer cannot compute the digest.</exception>
    private static byte[] Hash(PdfPreparedSignature prepared, PdfDigestMethod digest)
    {
        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var length = prepared.File.HashSigned(PdfSigningDigests.ToCms(digest), hash);
        return length == 0 ? throw new NotSupportedException($"This computer cannot compute the {PdfSigningDigests.DisplayName(digest)} digest.") : hash[..length].ToArray();
    }

    /// <summary>Plans a document timestamp: an invisible signature field on the first page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <returns>The plan.</returns>
    private static PdfSigningPlan PlanFor(PdfDocument document, PdfSigningOptions options)
    {
        var time = options.SigningTime ?? (options.Clock ?? TimeProvider.System).GetUtcNow();
        var name = PdfSigningFields.NextName(document.Objects, NamePrefix);
        var invisible = new PdfSignatureAppearance { ShowName = false, ShowReason = false, ShowLocation = false, ShowDate = false };
        return new(PdfSignatureFormat.Rfc3161, options.TimestampDigest, PdfMdpPermission.None, null, null, null, null, string.Empty, time, null, name, invisible);
    }
}
