// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace HyperPdf.Core.Tests.Remote;

/// <summary>A web server on this computer that serves one document, with or without support for range requests.</summary>
[DebuggerDisplay("RangeServer: {Address} ({Requests} requests)")]
internal sealed class RangeServer : IAsyncDisposable
{
    /// <summary>The prefix of a range header value.</summary>
    private const string RangePrefix = "bytes=";

    /// <summary>The document served.</summary>
    private readonly byte[] _content;

    /// <summary>Whether the server answers range requests with partial content.</summary>
    private readonly bool _supportsRanges;

    /// <summary>The listener.</summary>
    private readonly HttpListener _listener = new();

    /// <summary>Stops the loop that answers requests.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>The loop that answers requests.</summary>
    private readonly Task _loop;

    /// <summary>The number of requests answered.</summary>
    private int _requests;

    /// <summary>The number of body bytes sent.</summary>
    private long _bytesSent;

    /// <summary>Initializes a new instance of the <see cref="RangeServer"/> class.</summary>
    /// <param name="content">The document served.</param>
    /// <param name="supportsRanges">Whether the server answers range requests with partial content.</param>
    private RangeServer(byte[] content, bool supportsRanges)
    {
        _content = content;
        _supportsRanges = supportsRanges;
        var port = FreePort();
        Address = new($"http://127.0.0.1:{port}/document.pdf");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _loop = Task.Run(() => AnswerAsync(_stop.Token));
    }

    /// <summary>Gets the document's address.</summary>
    internal Uri Address { get; }

    /// <summary>Gets the number of requests answered so far.</summary>
    internal int Requests => Volatile.Read(ref _requests);

    /// <summary>Gets the number of body bytes sent so far.</summary>
    internal long BytesSent => Volatile.Read(ref _bytesSent);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException)
        {
            // The listener was closed while a request was being waited for.
        }

        _stop.Dispose();
    }

    /// <summary>Starts a server.</summary>
    /// <param name="content">The document served.</param>
    /// <param name="supportsRanges">Whether the server answers range requests with partial content.</param>
    /// <returns>The server.</returns>
    internal static RangeServer Start(byte[] content, bool supportsRanges) => new(content, supportsRanges);

    /// <summary>Finds a port nobody is using.</summary>
    /// <returns>The port.</returns>
    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>Answers requests until the server is stopped.</summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that ends when the server stops.</returns>
    private async Task AnswerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().ConfigureAwait(false);
            _ = Interlocked.Increment(ref _requests);
            await RespondAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Answers one request.</summary>
    /// <param name="context">The request and its response.</param>
    /// <param name="cancellationToken">Cancels the answer.</param>
    /// <returns>A task that ends when the response is sent.</returns>
    private async Task RespondAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var header = context.Request.Headers[nameof(Range)];
        var response = context.Response;
        if (!_supportsRanges || header is null || !header.StartsWith(RangePrefix, StringComparison.Ordinal))
        {
            _ = Interlocked.Add(ref _bytesSent, _content.Length);
            response.ContentLength64 = _content.Length;
            await response.OutputStream.WriteAsync(_content, cancellationToken).ConfigureAwait(false);
            response.Close();
            return;
        }

        var bounds = header[RangePrefix.Length..].Split('-');
        var first = long.Parse(bounds[0], CultureInfo.InvariantCulture);
        var last = Math.Min(long.Parse(bounds[1], CultureInfo.InvariantCulture), _content.Length - 1);
        response.StatusCode = (int)HttpStatusCode.PartialContent;
        response.AddHeader("Content-Range", string.Create(CultureInfo.InvariantCulture, $"bytes {first}-{last}/{_content.Length}"));
        response.ContentLength64 = last - first + 1;
        _ = Interlocked.Add(ref _bytesSent, last - first + 1);
        await response.OutputStream.WriteAsync(_content.AsMemory((int)first, (int)(last - first + 1)), cancellationToken).ConfigureAwait(false);
        response.Close();
    }
}
