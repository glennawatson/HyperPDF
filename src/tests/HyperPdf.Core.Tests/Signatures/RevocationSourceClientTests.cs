// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Http.Signatures;

namespace HyperPdf.Core.Tests.Signatures;

/// <summary>Tests for <see cref="RevocationSourceClient"/> against a recording HTTP handler.</summary>
public sealed class RevocationSourceClientTests
{
    /// <summary>The requests started together.</summary>
    private const int Parallelism = 16;

    /// <summary>An OCSP response that reports success and holds nothing else.</summary>
    private static readonly byte[] SuccessfulOcsp = [0x30, 0x03, 0x0A, 0x01, 0x00];

    /// <summary>Some bytes that stand in for a list, and are not a certificate.</summary>
    private static readonly byte[] CrlBytes = [0x30, 0x00];

    /// <summary>An OCSP response that reports the responder is busy.</summary>
    private static readonly byte[] BusyOcsp = [0x30, 0x03, 0x0A, 0x01, 0x03];

    /// <summary>The time limit that makes a hung responder fail quickly.</summary>
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>The address of the OCSP responder in the test certificates.</summary>
    private static readonly Uri ResponderAddress = new("http://ocsp.test.example/query");

    /// <summary>The address of the CRL in the test certificates.</summary>
    private static readonly Uri ListAddress = new("http://crl.test.example/root.crl");

    /// <summary>The address of the issuer in the test certificates.</summary>
    private static readonly Uri IssuerAddress = new("http://ca.test.example/root.cer");

    /// <summary>The ways a transport fails that leave a gap in the validation data.</summary>
    public enum FailureKind
    {
        /// <summary>The transport throws a timeout exception.</summary>
        Timeout = 0,

        /// <summary>The transport reports its own cancelled request, as an HTTP client does when its time limit passes.</summary>
        CancelledRequest = 1,

        /// <summary>The server cannot be reached.</summary>
        Unreachable = 2,
    }

    /// <summary>The OCSP request names the certificate and its issuer and goes to the responder the certificate names.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OcspRequestGoesToTheResponder(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        var handler = new RecordingHandler(SuccessfulOcsp);
        using var client = new RevocationSourceClient(handler);

        var reply = await client.GetOcspResponseAsync(certificates.Leaf.RawData, certificates.Root.RawData, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(reply).IsEquivalentTo(SuccessfulOcsp);
            await Assert.That(handler.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(handler.Uri).IsEqualTo(ResponderAddress);
            await Assert.That(handler.ContentType).IsEqualTo("application/ocsp-request");
            await Assert.That(Convert.ToHexString(handler.Body!)).Contains(certificates.Leaf.SerialNumber);
            await Assert.That(Convert.ToHexString(handler.Body!)).Contains(Convert.ToHexString(CryptographicOperations.HashData(HashAlgorithmName.SHA1, certificates.Leaf.IssuerName.RawData)));
        }
    }

    /// <summary>A response that is not successful is not returned.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsuccessfulOcspResponseIsIgnored(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        using var client = new RevocationSourceClient(new RecordingHandler(BusyOcsp));

        await Assert.That(await client.GetOcspResponseAsync(certificates.Leaf.RawData, certificates.Root.RawData, cancellationToken)).IsNull();
    }

    /// <summary>The CRL is fetched from the distribution point the certificate names.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CrlComesFromTheDistributionPoint(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        var handler = new RecordingHandler(CrlBytes);
        using var client = new RevocationSourceClient(handler);

        var reply = await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(reply).IsEquivalentTo(CrlBytes);
            await Assert.That(handler.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(handler.Uri).IsEqualTo(ListAddress);
        }
    }

    /// <summary>The issuer is fetched from the address the certificate names and must be a certificate.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task IssuerComesFromTheAuthorityInformation(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        var handler = new RecordingHandler(certificates.Root.RawData);
        using var client = new RevocationSourceClient(handler);
        var issuer = await client.GetIssuerAsync(certificates.Leaf.RawData, cancellationToken);
        using var notACertificate = new RevocationSourceClient(new RecordingHandler(CrlBytes));

        using (Assert.Multiple())
        {
            await Assert.That(issuer).IsEquivalentTo(certificates.Root.RawData);
            await Assert.That(handler.Uri).IsEqualTo(IssuerAddress);
            await Assert.That(await notACertificate.GetIssuerAsync(certificates.Leaf.RawData, cancellationToken)).IsNull();
        }
    }

    /// <summary>A server that is down leaves a gap and no error.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreachableServerGivesNothing(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        using var client = new RevocationSourceClient(new RecordingHandler([], HttpStatusCode.ServiceUnavailable));

        using (Assert.Multiple())
        {
            await Assert.That(await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken)).IsNull();
            await Assert.That(await client.GetOcspResponseAsync(certificates.Leaf.RawData, certificates.Root.RawData, cancellationToken)).IsNull();
        }
    }

    /// <summary>A responder that accepts the request and never answers gives nothing when the client's time limit passes, not an error.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResponderThatNeverAnswersGivesNothing(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        using var client = new RevocationSourceClient(new HangingHandler(), ShortTimeout);

        using (Assert.Multiple())
        {
            await Assert.That(await client.GetIssuerAsync(certificates.Leaf.RawData, cancellationToken)).IsNull();
            await Assert.That(await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken)).IsNull();
            await Assert.That(await client.GetOcspResponseAsync(certificates.Leaf.RawData, certificates.Root.RawData, cancellationToken)).IsNull();
        }
    }

    /// <summary>A timeout that the transport reports as a timeout or a cancelled request gives nothing too.</summary>
    /// <param name="kind">How the transport reports the timeout.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FailureKind.Timeout)]
    [Arguments(FailureKind.CancelledRequest)]
    [Arguments(FailureKind.Unreachable)]
    public async Task TransportFailureGivesNothing(FailureKind kind, CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        using var client = new RevocationSourceClient(new FailingHandler(kind));

        await Assert.That(await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken)).IsNull();
    }

    /// <summary>A caller that cancels still sees the cancellation, not an empty result.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CallersCancellationIsNotHidden(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        using var client = new RevocationSourceClient(new HangingHandler());
        using var cancelled = new CancellationTokenSource(ShortTimeout);

        await Assert.That(async () => await client.GetCrlAsync(certificates.Leaf.RawData, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(cancellationToken.IsCancellationRequested).IsFalse();
    }

    /// <summary>Requests that start together for one host all work, and a request after the dispose is refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParallelRequestsShareOneHostAndDisposeRefusesMore(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        var client = new RevocationSourceClient(new RecordingHandler(CrlBytes));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task<byte[]?>[Parallelism];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(
                async () =>
                {
                    await start.Task;
                    return await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken);
                },
                cancellationToken);
        }

        start.SetResult();
        var replies = await Task.WhenAll(tasks);
        client.Dispose();
        client.Dispose();

        await Assert.That(Array.TrueForAll(replies, static reply => reply is not null)).IsTrue();
        await Assert.That(async () => await client.GetCrlAsync(certificates.Leaf.RawData, cancellationToken)).Throws<ObjectDisposedException>();
    }

    /// <summary>A certificate that names no web address gives nothing and sends nothing.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertificateWithoutAddressesSendsNothing(CancellationToken cancellationToken)
    {
        using var certificates = new Chain();
        var handler = new RecordingHandler(SuccessfulOcsp);
        using var client = new RevocationSourceClient(handler);

        using (Assert.Multiple())
        {
            await Assert.That(await client.GetCrlAsync(certificates.Root.RawData, cancellationToken)).IsNull();
            await Assert.That(await client.GetIssuerAsync(certificates.Root.RawData, cancellationToken)).IsNull();
            await Assert.That(handler.Uri).IsNull();
        }
    }

    /// <summary>A root and a leaf whose extensions name the test addresses, plus an address that is not a web address.</summary>
    private sealed class Chain : IDisposable
    {
        /// <summary>The key size of the test keys.</summary>
        private const int KeyBits = 2048;

        /// <summary>The days the certificates are valid.</summary>
        private const int ValidDays = 30;

        /// <summary>Initializes a new instance of the <see cref="Chain"/> class.</summary>
        internal Chain()
        {
            using var rootKey = RSA.Create(KeyBits);
            var rootRequest = new CertificateRequest("CN=Revocation Test Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            var start = TimeProvider.System.GetUtcNow().AddDays(-1);
            Root = rootRequest.CreateSelfSigned(start, start.AddDays(ValidDays));
            using var leafKey = RSA.Create(KeyBits);
            var leafRequest = new CertificateRequest("CN=Revocation Test Leaf", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension([ResponderAddress.ToString(), "ldap://ocsp.test.example/query"], [IssuerAddress.ToString()]));
            leafRequest.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(["ldap://crl.test.example/root", ListAddress.ToString()]));
            Leaf = leafRequest.Create(Root, start, start.AddDays(ValidDays), RandomNumberGenerator.GetBytes(ValidDays));
        }

        /// <summary>Gets the root certificate.</summary>
        internal X509Certificate2 Root { get; }

        /// <summary>Gets the leaf certificate.</summary>
        internal X509Certificate2 Leaf { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Leaf.Dispose();
            Root.Dispose();
        }
    }

    /// <summary>A handler that never answers until the request is cancelled.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new();
        }
    }

    /// <summary>A handler whose transport fails in one of the ways a network does.</summary>
    /// <param name="kind">How it fails.</param>
    private sealed class FailingHandler(FailureKind kind) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => kind switch
        {
            FailureKind.Timeout => throw new TimeoutException("The transport timed out."),
            FailureKind.CancelledRequest => throw new TaskCanceledException("The request timed out.", new TimeoutException()),
            _ => throw new HttpRequestException("Unreachable."),
        };
    }

    /// <summary>Replies with fixed bytes and records the last request.</summary>
    /// <param name="reply">The bytes to reply with.</param>
    /// <param name="status">The status to reply with.</param>
    private sealed class RecordingHandler(byte[] reply, HttpStatusCode status) : HttpMessageHandler
    {
        /// <summary>Initializes a new instance of the <see cref="RecordingHandler"/> class.</summary>
        /// <param name="reply">The bytes to reply with.</param>
        public RecordingHandler(byte[] reply)
            : this(reply, HttpStatusCode.OK)
        {
        }

        /// <summary>Gets the method of the last request.</summary>
        public HttpMethod? Method { get; private set; }

        /// <summary>Gets the address of the last request.</summary>
        public Uri? Uri { get; private set; }

        /// <summary>Gets the content type of the last request.</summary>
        public string? ContentType { get; private set; }

        /// <summary>Gets the body of the last request.</summary>
        public byte[]? Body { get; private set; }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new(status) { Content = new ByteArrayContent(reply) };
        }
    }
}
