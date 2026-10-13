// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Signing;

/// <summary>Collects the certificates and revocation data each signature in a document needs for long-term validation.</summary>
internal static class PdfValidationDataCollector
{
    /// <summary>The signatureTimeStampToken unsigned attribute.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>The most issuer certificates fetched to complete one chain.</summary>
    private const int MaxFetches = 8;

    /// <summary>Collects the data for every signed signature.</summary>
    /// <param name="document">The document.</param>
    /// <param name="source">Finds revocation data and missing issuers.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The data for each signed signature, in the order of the document's signatures.</returns>
    internal static async ValueTask<IReadOnlyList<PdfSignatureValidationData>> CollectAsync(PdfDocument document, IPdfRevocationSource source, CancellationToken cancellationToken)
    {
        var stored = PdfDocumentSignatureValidation.GetSecurityStore(document).Certificates;
        var results = new List<PdfSignatureValidationData>();
        foreach (var field in PdfDocumentAttachments.GetSignatures(document))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Decode(field.Contents) is not { } container)
            {
                continue;
            }

            var gathered = new Gathered();
            var pool = new X509Certificate2Collection();
            pool.AddRange(container.Certificates);
            foreach (var raw in stored)
            {
                _ = pool.Add(X509CertificateLoader.LoadCertificate(raw));
            }

            await AddChainAsync(container.SignerInfos[0].Certificate, pool, source, gathered, cancellationToken).ConfigureAwait(false);
            await AddTimestampsAsync(container.SignerInfos[0], pool, source, gathered, cancellationToken).ConfigureAwait(false);
            results.Add(new(field.Contents, [.. gathered.Certificates], [.. gathered.Ocsps], [.. gathered.Crls]));
        }

        return results;
    }

    /// <summary>Decodes a signature value as a container, ignoring the zero padding after it.</summary>
    /// <param name="contents">The /Contents value.</param>
    /// <returns>The container, or <see langword="null"/> when there is none or it cannot be read.</returns>
    private static SignedCms? Decode(byte[] contents)
    {
        if (contents.Length == 0)
        {
            return null;
        }

        try
        {
            var length = ContainerLength(contents);
            var cms = new SignedCms();
            cms.Decode(contents.AsSpan(0, length));
            return cms.SignerInfos.Count == 0 ? null : cms;
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException)
        {
            return null;
        }
    }

    /// <summary>Reads the length of the DER value at the start of some bytes.</summary>
    /// <param name="contents">The bytes.</param>
    /// <returns>The length, which leaves out any padding after the value.</returns>
    /// <exception cref="AsnContentException">The bytes do not start with an encoded value.</exception>
    private static int ContainerLength(byte[] contents)
    {
        _ = AsnDecoder.ReadEncodedValue(contents, AsnEncodingRules.BER, out _, out _, out var length);
        return length;
    }

    /// <summary>Adds the chain of each signature timestamp on a signer.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="pool">The certificates available to build chains.</param>
    /// <param name="source">Finds revocation data and missing issuers.</param>
    /// <param name="gathered">Receives the data.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>A task that completes when the chains are added.</returns>
    private static async ValueTask AddTimestampsAsync(SignerInfo signer, X509Certificate2Collection pool, IPdfRevocationSource source, Gathered gathered, CancellationToken cancellationToken)
    {
        foreach (var attribute in signer.UnsignedAttributes)
        {
            if (attribute.Oid.Value != TimestampTokenOid || attribute.Values.Count == 0 || !Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var token, out _))
            {
                continue;
            }

            var timestamp = token.AsSignedCms();
            pool.AddRange(timestamp.Certificates);
            await AddChainAsync(timestamp.SignerInfos[0].Certificate, pool, source, gathered, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Adds a certificate's chain and the revocation data for each certificate in it that has an issuer.</summary>
    /// <param name="leaf">The certificate, or <see langword="null"/>.</param>
    /// <param name="pool">The certificates available to build the chain.</param>
    /// <param name="source">Finds revocation data and missing issuers.</param>
    /// <param name="gathered">Receives the data.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>A task that completes when the chain is added.</returns>
    private static async ValueTask AddChainAsync(X509Certificate2? leaf, X509Certificate2Collection pool, IPdfRevocationSource source, Gathered gathered, CancellationToken cancellationToken)
    {
        if (leaf is null)
        {
            return;
        }

        var chain = await BuildAsync(leaf, pool, source, cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < chain.Count; i++)
        {
            gathered.AddCertificate(chain[i].RawData);
            if (i + 1 >= chain.Count)
            {
                continue;
            }

            var ocsp = await source.GetOcspResponseAsync(chain[i].RawData, chain[i + 1].RawData, cancellationToken).ConfigureAwait(false);
            var crl = ocsp is null ? await source.GetCrlAsync(chain[i].RawData, cancellationToken).ConfigureAwait(false) : null;
            gathered.AddRevocation(ocsp, crl);
        }
    }

    /// <summary>Builds the chain of a certificate, fetching issuers that are missing.</summary>
    /// <param name="leaf">The certificate.</param>
    /// <param name="pool">The certificates available; fetched issuers are added to it.</param>
    /// <param name="source">Finds missing issuers.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The chain, the certificate first.</returns>
    private static async ValueTask<List<X509Certificate2>> BuildAsync(X509Certificate2 leaf, X509Certificate2Collection pool, IPdfRevocationSource source, CancellationToken cancellationToken)
    {
        var chain = PdfCertificateChains.Build(leaf, pool);
        for (var fetches = 0; fetches < MaxFetches && !PdfCertificateChains.IsSelfSigned(chain[^1]); fetches++)
        {
            var issuer = await source.GetIssuerAsync(chain[^1].RawData, cancellationToken).ConfigureAwait(false);
            if (issuer is null)
            {
                break;
            }

            _ = pool.Add(X509CertificateLoader.LoadCertificate(issuer));
            chain = PdfCertificateChains.Build(leaf, pool);
        }

        return chain;
    }

    /// <summary>The validation data gathered for one signature, without repeats.</summary>
    private sealed class Gathered
    {
        /// <summary>The hashes of the certificates added.</summary>
        private readonly HashSet<string> _certificateHashes = [with(StringComparer.Ordinal)];

        /// <summary>The hashes of the OCSP responses added.</summary>
        private readonly HashSet<string> _ocspHashes = [with(StringComparer.Ordinal)];

        /// <summary>The hashes of the lists added.</summary>
        private readonly HashSet<string> _crlHashes = [with(StringComparer.Ordinal)];

        /// <summary>Gets the certificates.</summary>
        internal List<byte[]> Certificates { get; } = [];

        /// <summary>Gets the OCSP responses.</summary>
        internal List<byte[]> Ocsps { get; } = [];

        /// <summary>Gets the revocation lists.</summary>
        internal List<byte[]> Crls { get; } = [];

        /// <summary>Adds a certificate.</summary>
        /// <param name="der">The DER certificate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddCertificate(byte[] der) => AddOnce(Certificates, _certificateHashes, der);

        /// <summary>Adds the revocation data found for a certificate.</summary>
        /// <param name="ocsp">The OCSP response, or <see langword="null"/>.</param>
        /// <param name="crl">The list, or <see langword="null"/>.</param>
        internal void AddRevocation(byte[]? ocsp, byte[]? crl)
        {
            if (ocsp is not null)
            {
                AddOnce(Ocsps, _ocspHashes, ocsp);
            }

            if (crl is not null)
            {
                AddOnce(Crls, _crlHashes, crl);
            }
        }

        /// <summary>Adds an item unless it is already in the list.</summary>
        /// <param name="list">The list.</param>
        /// <param name="hashes">The hashes of what the list holds.</param>
        /// <param name="item">The item.</param>
        private static void AddOnce(List<byte[]> list, HashSet<string> hashes, byte[] item)
        {
            if (hashes.Add(Convert.ToHexString(SHA256.HashData(item))))
            {
                list.Add(item);
            }
        }
    }
}
