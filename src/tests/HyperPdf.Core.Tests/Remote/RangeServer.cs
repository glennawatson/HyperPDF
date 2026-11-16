// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Core.Tests.Remote;

/// <summary>
/// A web server on this computer that serves one document, with or without support for range requests. It listens on a port the system picks as the
/// socket is opened, so no other test or test run can take the port first, and each connection is answered on its own, so a client that
/// hangs up in the middle of an answer cannot stop the server answering the next request.
/// </summary>
[DebuggerDisplay("RangeServer: {Address} ({Requests} requests)")]
internal sealed class RangeServer : IAsyncDisposable
{
    /// <summary>The prefix of a range header value.</summary>
    private const string RangePrefix = "bytes=";

    /// <summary>The name of the header that asks for part of the document.</summary>
    private const string RangeHeader = "Range:";

    /// <summary>The most bytes of a request that are read before the request is given up on.</summary>
    private const int MaxRequestBytes = 16 * 1024;

    /// <summary>The document served.</summary>
    private readonly byte[] _content;

    /// <summary>Whether the server answers range requests with partial content.</summary>
    private readonly bool _supportsRanges;

    /// <summary>The listener.</summary>
    private readonly TcpListener _listener;

    /// <summary>Stops the loop that accepts connections and the answers in progress.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>The loop that accepts connections.</summary>
    private readonly Task _loop;

    /// <summary>The answers in progress, so disposing waits for them.</summary>
    private readonly ConcurrentDictionary<Task, byte> _answers = new();

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
        _listener = new(IPAddress.Loopback, 0);
        _listener.Start();
        Address = new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/document.pdf");
        _loop = Task.Run(() => AcceptAsync(_stop.Token));
    }

    /// <summary>Gets the document's address.</summary>
    internal Uri Address { get; }

    /// <summary>Gets the number of requests answered so far.</summary>
    internal int Requests => Volatile.Read(ref _requests);

    /// <summary>Gets the number of body bytes sent so far.</summary>
    internal long BytesSent => Volatile.Read(ref _bytesSent);

    /// <summary>Gets the bytes that end the headers of a request.</summary>
    private static ReadOnlySpan<byte> EndOfHeaders => "\r\n\r\n"u8;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The listener was stopped while a connection was being waited for.
        }

        await Task.WhenAll(_answers.Keys).ConfigureAwait(false);
        _stop.Dispose();
    }

    /// <summary>Starts a server.</summary>
    /// <param name="content">The document served.</param>
    /// <param name="supportsRanges">Whether the server answers range requests with partial content.</param>
    /// <returns>The server.</returns>
    internal static RangeServer Start(byte[] content, bool supportsRanges) => new(content, supportsRanges);

    /// <summary>Reads the request line and headers of a request.</summary>
    /// <param name="stream">The connection.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request's text, or <see langword="null"/> when the connection ended before the headers did.</returns>
    private static async Task<string?> ReadHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var buffer = new byte[1024];
        while (received.Count < MaxRequestBytes)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            received.AddRange(buffer.AsSpan(0, read));
            if (CollectionsMarshal.AsSpan(received).IndexOf(EndOfHeaders) >= 0)
            {
                return Encoding.ASCII.GetString([.. received]);
            }
        }

        return null;
    }

    /// <summary>Accepts connections until the server is stopped, answering each on its own.</summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that ends when the server stops.</returns>
    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            var answer = AnswerAsync(client, cancellationToken);
            _ = _answers.TryAdd(answer, 0);
            _ = answer.ContinueWith(
                static (done, answers) => ((ConcurrentDictionary<Task, byte>)answers!).TryRemove(done, out _),
                _answers,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>Answers the one request of a connection. A client that hangs up, or a request that cannot be read, ends only this answer.</summary>
    /// <param name="client">The connection.</param>
    /// <param name="cancellationToken">Cancels the answer.</param>
    /// <returns>A task that ends when the answer is sent or given up.</returns>
    private async Task AnswerAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null)
                {
                    return;
                }

                _ = Interlocked.Increment(ref _requests);
                await RespondAsync(stream, request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
            {
                // The client went away in the middle of the answer, or the server is stopping.
            }
        }
    }

    /// <summary>Answers one request: the whole document, or the part a range header asks for when ranges are supported.</summary>
    /// <param name="stream">The connection.</param>
    /// <param name="request">The request's text.</param>
    /// <param name="cancellationToken">Cancels the answer.</param>
    /// <returns>A task that ends when the response is sent.</returns>
    private async Task RespondAsync(NetworkStream stream, string request, CancellationToken cancellationToken)
    {
        var headOnly = request.StartsWith("HEAD ", StringComparison.Ordinal);
        var range = FindRange(request);
        if (!_supportsRanges || range is null)
        {
            await SendAsync(stream, "200 OK", string.Empty, _content, headOnly, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (first, last) = range.Value;
        last = Math.Min(last, _content.Length - 1);
        var part = _content.AsMemory((int)first, (int)(last - first + 1));
        var contentRange = string.Create(CultureInfo.InvariantCulture, $"Content-Range: bytes {first}-{last}/{_content.Length}\r\n");
        await SendAsync(stream, "206 Partial Content", contentRange, part, headOnly, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds the bytes a request asks for.</summary>
    /// <param name="request">The request's text.</param>
    /// <returns>The first and last byte, or <see langword="null"/> when the request has no range.</returns>
    private (long First, long Last)? FindRange(string request)
    {
        foreach (var line in request.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(RangeHeader, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[RangeHeader.Length..].Trim();
            if (!value.StartsWith(RangePrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var bounds = value[RangePrefix.Length..].Split('-');
            var first = long.Parse(bounds[0], CultureInfo.InvariantCulture);
            var last = bounds.Length > 1 && bounds[1].Length > 0 ? long.Parse(bounds[1], CultureInfo.InvariantCulture) : _content.Length - 1;
            return (first, last);
        }

        return null;
    }

    /// <summary>Sends a response and counts what was sent.</summary>
    /// <param name="stream">The connection.</param>
    /// <param name="status">The status line's text.</param>
    /// <param name="extraHeaders">Headers to add, each ending with a line break.</param>
    /// <param name="body">The body.</param>
    /// <param name="headOnly">Whether only the headers are sent.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that ends when the response is sent.</returns>
    private async Task SendAsync(NetworkStream stream, string status, string extraHeaders, ReadOnlyMemory<byte> body, bool headOnly, CancellationToken cancellationToken)
    {
        var headers = string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {status}\r\nContent-Length: {body.Length}\r\n{extraHeaders}Connection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken).ConfigureAwait(false);
        if (headOnly)
        {
            return;
        }

        _ = Interlocked.Add(ref _bytesSent, body.Length);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }
}
