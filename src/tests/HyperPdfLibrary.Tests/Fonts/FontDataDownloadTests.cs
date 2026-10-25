// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using HyperPdfLibrary.Fonts.Generation;
using Refit;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Recovers bounded transport faults without hiding permanent errors or cancellation.</summary>
public sealed class FontDataDownloadTests
{
    /// <summary>The fake generated endpoint.</summary>
    private const string ResourcePath = "pinned/font.bin";

    /// <summary>The attempt that succeeds after one transient failure.</summary>
    private const int SecondAttempt = 2;

    /// <summary>The maximum time to observe an active scripted body.</summary>
    private const int ObservationSeconds = 5;

    /// <summary>The complete scripted response.</summary>
    private static readonly byte[] CompleteBody = [1, 0];

    /// <summary>A generated Refit send error is replayed and returns the complete second response.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConnectionResetThenSuccess()
    {
        using var content = new FontDownloadContent(null, false);
        using var handler = new FontDownloadHandler(Reset(), Response(HttpStatusCode.OK, content));
        using var client = Client(handler);
        var result = await ReadAsync(client, CancellationToken.None);
        await Assert.That(result).IsEquivalentTo(CompleteBody);
        await Assert.That(handler.Attempts).IsEqualTo(SecondAttempt);
        await Assert.That(content.IsDisposed).IsTrue();
    }

    /// <summary>Repeated reset faults exhaust the fixed attempt budget and preserve the final Refit failure.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetsExhaustAttempts()
    {
        var last = Reset();
        using var handler = new FontDownloadHandler(Reset(), Reset(), last);
        using var client = Client(handler);
        ApiRequestException? failure = null;
        try
        {
            _ = await ReadAsync(client, CancellationToken.None);
        }
        catch (ApiRequestException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure?.InnerException).IsSameReferenceAs(last);
        await Assert.That(handler.Attempts).IsEqualTo(FontDataDownload.MaximumAttempts);
    }

    /// <summary>Transient HTTP statuses dispose their response before a fresh successful GET.</summary>
    /// <param name="status">The recoverable upstream status.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(HttpStatusCode.RequestTimeout)]
    [Arguments(HttpStatusCode.TooManyRequests)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.BadGateway)]
    [Arguments(HttpStatusCode.ServiceUnavailable)]
    [Arguments(HttpStatusCode.GatewayTimeout)]
    public async Task TransientHttpThenSuccess(HttpStatusCode status)
    {
        using var failed = new FontDownloadContent(null, false);
        using var complete = new FontDownloadContent(null, false);
        using var handler = new FontDownloadHandler(Response(status, failed), Response(HttpStatusCode.OK, complete));
        using var client = Client(handler);
        await Assert.That(await ReadAsync(client, CancellationToken.None)).IsEquivalentTo(CompleteBody);
        await Assert.That(handler.Attempts).IsEqualTo(SecondAttempt);
        await Assert.That(failed.IsDisposed).IsTrue();
        await Assert.That(complete.IsDisposed).IsTrue();
    }

    /// <summary>Cancellation before acquisition sends no request.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreCancelledRequest()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var handler = new FontDownloadHandler();
        using var client = Client(handler);
        await Assert.That(async () => await ReadAsync(client, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(handler.Attempts).IsEqualTo(0);
    }

    /// <summary>A partial body is discarded and disposed before a fresh GET succeeds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialBodyResetThenSuccess()
    {
        using var partial = new FontDownloadContent(new IOException("reset", new SocketException((int)SocketError.ConnectionReset)), false);
        using var complete = new FontDownloadContent(null, false);
        using var handler = new FontDownloadHandler(Response(HttpStatusCode.OK, partial), Response(HttpStatusCode.OK, complete));
        using var client = Client(handler);
        var result = await ReadAsync(client, CancellationToken.None);
        await Assert.That(result).IsEquivalentTo(CompleteBody);
        await Assert.That(handler.Attempts).IsEqualTo(SecondAttempt);
        await Assert.That(partial.IsDisposed).IsTrue();
        await Assert.That(complete.IsDisposed).IsTrue();
    }

    /// <summary>Every truncated body is disposed, including the final exhausted attempt.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialBodiesExhaustAttempts()
    {
        using var first = new FontDownloadContent(new HttpIOException(HttpRequestError.ResponseEnded), false);
        using var second = new FontDownloadContent(new HttpIOException(HttpRequestError.ResponseEnded), false);
        using var last = new FontDownloadContent(new HttpIOException(HttpRequestError.ResponseEnded), false);
        using var handler = new FontDownloadHandler(Response(HttpStatusCode.OK, first), Response(HttpStatusCode.OK, second), Response(HttpStatusCode.OK, last));
        using var client = Client(handler);
        await Assert.That(async () => await ReadAsync(client, CancellationToken.None)).Throws<HttpRequestException>();
        await Assert.That(handler.Attempts).IsEqualTo(FontDataDownload.MaximumAttempts);
        await Assert.That(first.IsDisposed).IsTrue();
        await Assert.That(second.IsDisposed).IsTrue();
        await Assert.That(last.IsDisposed).IsTrue();
    }

    /// <summary>Caller cancellation interrupts the first backoff without another GET.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationDuringBackoff()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new FontDownloadHandler(Reset());
        using var client = Client(handler);
        var pending = ReadAsync(client, cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        await Assert.That(handler.Attempts).IsEqualTo(1);
    }

    /// <summary>Cancellation of an active partial body disposes its response and never retries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationDuringBody()
    {
        using var cancellation = new CancellationTokenSource();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(ObservationSeconds));
        using var content = new FontDownloadContent(null, true);
        using var handler = new FontDownloadHandler(Response(HttpStatusCode.OK, content));
        using var client = Client(handler);
        var pending = ReadAsync(client, cancellation.Token).AsTask();
        await content.Started.WaitAsync(bound.Token);
        await cancellation.CancelAsync();
        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        await Assert.That(handler.Attempts).IsEqualTo(1);
        await Assert.That(content.IsDisposed).IsTrue();
    }

    /// <summary>A missing resource fails immediately and disposes its error response.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PermanentHttpFailure()
    {
        using var content = new FontDownloadContent(null, false);
        using var handler = new FontDownloadHandler(Response(HttpStatusCode.NotFound, content));
        using var client = Client(handler);
        await Assert.That(async () => await ReadAsync(client, CancellationToken.None)).Throws<HttpRequestException>();
        await Assert.That(handler.Attempts).IsEqualTo(1);
        await Assert.That(content.IsDisposed).IsTrue();
    }

    /// <summary>An arbitrary body I/O failure propagates without retrying or retaining the partial response.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PermanentBodyFailure()
    {
        using var content = new FontDownloadContent(new IOException("invalid content"), false);
        using var handler = new FontDownloadHandler(Response(HttpStatusCode.OK, content));
        using var client = Client(handler);
        await Assert.That(async () => await ReadAsync(client, CancellationToken.None)).Throws<HttpRequestException>();
        await Assert.That(handler.Attempts).IsEqualTo(1);
        await Assert.That(content.IsDisposed).IsTrue();
    }

    /// <summary>Classifies response truncation but rejects TLS, protocol and arbitrary I/O failures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureClassification()
    {
        await Assert.That(FontDataDownload.IsTransient(new HttpIOException(HttpRequestError.ResponseEnded))).IsTrue();
        await Assert.That(FontDataDownload.IsTransient(new HttpRequestException(HttpRequestError.SecureConnectionError))).IsFalse();
        await Assert.That(FontDataDownload.IsTransient(new HttpRequestException(HttpRequestError.InvalidResponse))).IsFalse();
        await Assert.That(FontDataDownload.IsTransient(new IOException("content"))).IsFalse();
        await Assert.That(FontDataDownload.IsTransient(new OperationCanceledException())).IsFalse();
    }

    /// <summary>Uses the production generated Refit operation with a private injected transport.</summary>
    /// <param name="client">The private HTTP client.</param>
    /// <param name="token">Cancels the whole operation.</param>
    /// <returns>The complete resource bytes.</returns>
    private static ValueTask<byte[]> ReadAsync(HttpClient client, CancellationToken token) =>
        FontDataDownload.ReadAsync(RestService.ForGenerated<IFontDataApi>(client, FontDataJsonContext.Default), ResourcePath, token);

    /// <summary>Creates a client without real network access.</summary>
    /// <param name="handler">The scripted transport.</param>
    /// <returns>The private client.</returns>
    private static HttpClient Client(FontDownloadHandler handler) => new(handler) { BaseAddress = new("https://example.invalid") };

    /// <summary>Creates the exact reset chain observed in CI.</summary>
    /// <returns>The send failure.</returns>
    private static HttpRequestException Reset() => new("reset", new IOException("transport", new SocketException((int)SocketError.ConnectionReset)));

    /// <summary>Creates an owned response with scripted body behavior.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="content">The response body.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage Response(HttpStatusCode status, FontDownloadContent content) => new(status) { Content = content };
}
