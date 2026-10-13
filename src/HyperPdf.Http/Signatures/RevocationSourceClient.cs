// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;
using Refit;

namespace HyperPdf.Http.Signatures;

/// <summary>
/// Finds revocation data over HTTP from the addresses a certificate names: the OCSP responder and issuer in its authority
/// information access extension, and the list in its CRL distribution points extension (RFC 5280, 4.2.2.1 and 4.2.1.13).
/// Only <c>http</c> and <c>https</c> addresses are used, and a reply is read only up to a size limit.
/// </summary>
[DebuggerDisplay("RevocationSourceClient")]
public sealed class RevocationSourceClient : ISignatureRevocationSource, IDisposable
{
    /// <summary>The most bytes read from one reply.</summary>
    private const int MaxReplyBytes = 4_194_304;

    /// <summary>The bytes read from a reply at a time.</summary>
    private const int ChunkBytes = 16_384;

    /// <summary>How long a server may take to answer.</summary>
    private const int TimeoutSeconds = 15;

    /// <summary>The object identifier of the SHA-1 digest, which RFC 6960 interoperability requires for the certificate identifier.</summary>
    private const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>The object identifier of the CRL distribution points extension.</summary>
    private const string CrlDistributionPointsOid = "2.5.29.31";

    /// <summary>The ASN.1 tag number of a uniformResourceIdentifier general name.</summary>
    private const int UriTag = 6;

    /// <summary>The ASN.1 tag number of the distributionPoint field of a distribution point.</summary>
    private const int DistributionPointTag = 0;

    /// <summary>The ASN.1 tag number of the fullName choice of a distribution point name.</summary>
    private const int FullNameTag = 0;

    /// <summary>The value of the responseStatus <c>successful</c> in an OCSP response.</summary>
    private const int OcspSuccessful = 0;

    /// <summary>The clients, one for each host, which Refit needs because it resolves paths against a base address.</summary>
    private readonly ConcurrentDictionary<string, Lazy<IRevocationApi>> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The handler every client sends through, or <see langword="null"/> for the default.</summary>
    private readonly HttpMessageHandler? _handler;

    /// <summary>The HTTP clients created, which are disposed with this object.</summary>
    private readonly ConcurrentBag<HttpClient> _httpClients = [];

    /// <summary>How long a server may take to answer.</summary>
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(TimeoutSeconds);

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="RevocationSourceClient"/> class.</summary>
    public RevocationSourceClient()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RevocationSourceClient"/> class.</summary>
    /// <param name="handler">The handler every request is sent through; it is not disposed with this object.</param>
    public RevocationSourceClient(HttpMessageHandler handler) => _handler = handler;

    /// <summary>Initializes a new instance of the <see cref="RevocationSourceClient"/> class with its own limit on how long a server may take.</summary>
    /// <param name="handler">The handler every request is sent through; it is not disposed with this object.</param>
    /// <param name="timeout">How long a server may take to answer.</param>
    internal RevocationSourceClient(HttpMessageHandler handler, TimeSpan timeout)
    {
        _handler = handler;
        _timeout = timeout;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var client in _httpClients)
        {
            client.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetOcspResponseAsync(byte[] certificate, byte[] issuer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(issuer);
        using var subject = X509CertificateLoader.LoadCertificate(certificate);
        using var authority = X509CertificateLoader.LoadCertificate(issuer);
        var request = BuildOcspRequest(subject, authority);
        foreach (var address in OcspAddresses(subject))
        {
            using var content = new ByteArrayContent(request);
            content.Headers.ContentType = new("application/ocsp-request");
            var reply = await SendAsync(address, content, cancellationToken).ConfigureAwait(false);
            if (reply is not null && IsSuccessful(reply))
            {
                return reply;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetCrlAsync(byte[] certificate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var subject = X509CertificateLoader.LoadCertificate(certificate);
        foreach (var address in CrlAddresses(subject))
        {
            if (await SendAsync(address, null, cancellationToken).ConfigureAwait(false) is { } list)
            {
                return list;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetIssuerAsync(byte[] certificate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var subject = X509CertificateLoader.LoadCertificate(certificate);
        foreach (var address in IssuerAddresses(subject))
        {
            if (await SendAsync(address, null, cancellationToken).ConfigureAwait(false) is { } issuer && IsCertificate(issuer))
            {
                return issuer;
            }
        }

        return null;
    }

    /// <summary>Builds the DER OCSP request for one certificate (RFC 6960, 4.1.1).</summary>
    /// <param name="subject">The certificate.</param>
    /// <param name="issuer">The certificate's issuer.</param>
    /// <returns>The request.</returns>
    internal static byte[] BuildOcspRequest(X509Certificate2 subject, X509Certificate2 issuer)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        using (writer.PushSequence())
                        {
                            using (writer.PushSequence())
                            {
                                writer.WriteObjectIdentifier(Sha1Oid);
                                writer.WriteNull();
                            }

                            // The format fixes SHA-1 for the certificate identifier; it only names the certificate and protects nothing.
                            writer.WriteOctetString(CryptographicOperations.HashData(HashAlgorithmName.SHA1, subject.IssuerName.RawData));
                            writer.WriteOctetString(CryptographicOperations.HashData(HashAlgorithmName.SHA1, issuer.PublicKey.EncodedKeyValue.RawData));
                            writer.WriteIntegerUnsigned(Convert.FromHexString(subject.SerialNumber));
                        }
                    }
                }
            }
        }

        return writer.Encode();
    }

    /// <summary>Lists the OCSP responder addresses a certificate names.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The addresses.</returns>
    private static List<Uri> OcspAddresses(X509Certificate2 certificate)
    {
        var addresses = new List<Uri>();
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509AuthorityInformationAccessExtension access)
            {
                AddWeb(addresses, access.EnumerateOcspUris());
            }
        }

        return addresses;
    }

    /// <summary>Lists the issuer certificate addresses a certificate names.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The addresses.</returns>
    private static List<Uri> IssuerAddresses(X509Certificate2 certificate)
    {
        var addresses = new List<Uri>();
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509AuthorityInformationAccessExtension access)
            {
                AddWeb(addresses, access.EnumerateCAIssuersUris());
            }
        }

        return addresses;
    }

    /// <summary>Lists the CRL addresses a certificate names in its distribution points.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The addresses.</returns>
    private static List<Uri> CrlAddresses(X509Certificate2 certificate)
    {
        var addresses = new List<Uri>();
        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value == CrlDistributionPointsOid)
            {
                ReadDistributionPoints(extension.RawData, addresses);
            }
        }

        return addresses;
    }

    /// <summary>Reads the web addresses out of a CRL distribution points extension.</summary>
    /// <param name="data">The extension's DER value.</param>
    /// <param name="addresses">Receives the addresses.</param>
    private static void ReadDistributionPoints(byte[] data, List<Uri> addresses)
    {
        try
        {
            var points = new AsnReader(data, AsnEncodingRules.DER).ReadSequence();
            while (points.HasData)
            {
                var point = points.ReadSequence();
                if (!point.HasData || !point.PeekTag().HasSameClassAndValue(new(TagClass.ContextSpecific, DistributionPointTag)))
                {
                    continue;
                }

                var name = point.ReadSequence(new(TagClass.ContextSpecific, DistributionPointTag));
                if (name.HasData && name.PeekTag().HasSameClassAndValue(new(TagClass.ContextSpecific, FullNameTag)))
                {
                    AddNames(name.ReadSequence(new(TagClass.ContextSpecific, FullNameTag)), addresses);
                }
            }
        }
        catch (AsnContentException)
        {
            // A damaged extension names no addresses; the certificate's other sources are still tried.
        }
    }

    /// <summary>Adds the web addresses among some general names.</summary>
    /// <param name="names">The general names.</param>
    /// <param name="addresses">Receives the addresses.</param>
    private static void AddNames(AsnReader names, List<Uri> addresses)
    {
        while (names.HasData)
        {
            if (names.PeekTag().HasSameClassAndValue(new(TagClass.ContextSpecific, UriTag)))
            {
                var text = names.ReadCharacterString(UniversalTagNumber.IA5String, new(TagClass.ContextSpecific, UriTag));
                AddWeb(addresses, [text]);
            }
            else
            {
                _ = names.ReadEncodedValue();
            }
        }
    }

    /// <summary>Adds the addresses that are absolute web addresses.</summary>
    /// <param name="addresses">Receives the addresses.</param>
    /// <param name="candidates">The texts to try.</param>
    private static void AddWeb(List<Uri> addresses, IEnumerable<string> candidates)
    {
        foreach (var text in candidates)
        {
            if (Uri.TryCreate(text, UriKind.Absolute, out var address) && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps))
            {
                addresses.Add(address);
            }
        }
    }

    /// <summary>Determines whether an OCSP response reports success (RFC 6960, 4.2.1).</summary>
    /// <param name="response">The DER response.</param>
    /// <returns><see langword="true"/> when the response status is successful.</returns>
    private static bool IsSuccessful(byte[] response)
    {
        try
        {
            var reader = new AsnReader(response, AsnEncodingRules.DER).ReadSequence();
            var status = reader.ReadEnumeratedBytes();
            return status.Length == 1 && status.Span[0] == OcspSuccessful;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    /// <summary>Determines whether some bytes are a DER certificate.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns><see langword="true"/> when they load as a certificate.</returns>
    private static bool IsCertificate(byte[] data)
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificate(data);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Tells whether a failure only means the responder gave no data: it was unreachable, answered badly or timed out.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when the failure is a gap in the data and not a fault in this program.</returns>
    private static bool IsGap(Exception exception) =>
        exception is HttpRequestException or ApiException or ApiRequestException or TaskCanceledException or TimeoutException;

    /// <summary>Reads a reply up to the size limit.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body, or <see langword="null"/> when the reply is too big.</returns>
    private static async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxReplyBytes)
        {
            return null;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await using var body = new MemoryStream();
            var buffer = new byte[ChunkBytes];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (body.Length + read > MaxReplyBytes)
                {
                    return null;
                }

                body.Write(buffer, 0, read);
            }

            return body.ToArray();
        }
    }

    /// <summary>Sends a request and reads the reply, treating any network or server failure as no data.</summary>
    /// <param name="address">The address.</param>
    /// <param name="content">The body of an OCSP request, or <see langword="null"/> for a plain fetch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply, or <see langword="null"/> when none could be had.</returns>
    private async Task<byte[]?> SendAsync(Uri address, HttpContent? content, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        // The client is made once for a host, even when requests for it start together.
        var api = _clients.GetOrAdd(address.GetLeftPart(UriPartial.Authority), static (authority, owner) => new(() => owner.CreateClient(authority)), this).Value;
        var path = address.PathAndQuery.TrimStart('/');
        try
        {
            using var response = content is null
                ? await api.FetchAsync(path, cancellationToken).ConfigureAwait(false)
                : await api.RequestOcspAsync(path, content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? await ReadLimitedAsync(response, cancellationToken).ConfigureAwait(false) : null;
        }
        catch (Exception ex) when (IsGap(ex))
        {
            // A responder that cannot be reached, or that never answers, leaves a gap in the validation data, which the
            // caller reports; it is not an error here. A caller that cancelled still sees the cancellation.
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    /// <summary>Creates the client of a host.</summary>
    /// <param name="authority">The scheme and host.</param>
    /// <returns>The client.</returns>
    /// <exception cref="ObjectDisposedException">This object was disposed while the client was made.</exception>
    private IRevocationApi CreateClient(string authority)
    {
        var httpClient = _handler is null
            ? new HttpClient { BaseAddress = new(authority), Timeout = _timeout }
            : new HttpClient(_handler, false) { BaseAddress = new(authority), Timeout = _timeout };
        _httpClients.Add(httpClient);
        if (Volatile.Read(ref _disposed) != 0)
        {
            // Dispose may have listed the clients before this one was added.
            httpClient.Dispose();
            throw new ObjectDisposedException(GetType().FullName);
        }

        return RefitClients.CreateRevocationApi(httpClient);
    }
}
