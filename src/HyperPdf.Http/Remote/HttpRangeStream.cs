// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace HyperPdf.Http.Remote;

/// <summary>
/// A read-only, seekable stream over a document on a web server, filled by range requests so only the parts that are read cross the
/// network. A PDF reader that opens it reads the end of the file for the cross-reference table first and then each page's objects,
/// so a large file shows its first page after a few small requests instead of after the whole download.
/// </summary>
/// <remarks>
/// This talks to <see cref="HttpClient"/> directly, not through Refit, because the library's byte source needs a synchronous read
/// as well as an asynchronous one and Refit only has asynchronous calls.
/// </remarks>
[DebuggerDisplay("HttpRangeStream: {_address} at {_position} of {Length}")]
public sealed class HttpRangeStream : Stream
{
    /// <summary>The unit of a range request.</summary>
    private const string RangeUnit = "bytes";

    /// <summary>The size of the buffer used to read a response that must be skipped past.</summary>
    private const int DiscardLength = 4096;

    /// <summary>The client, which this stream owns.</summary>
    private readonly HttpClient _client;

    /// <summary>The document's address.</summary>
    private readonly Uri _address;

    /// <summary>The current position.</summary>
    private long _position;

    /// <summary>Initializes a new instance of the <see cref="HttpRangeStream"/> class.</summary>
    /// <param name="client">The client, which the stream disposes.</param>
    /// <param name="address">The document's address.</param>
    /// <param name="length">The document's length in bytes.</param>
    private HttpRangeStream(HttpClient client, Uri address, long length)
    {
        _client = client;
        _address = address;
        Length = length;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length { get; }

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>Opens a document if its server answers range requests.</summary>
    /// <param name="address">The document's absolute <c>http</c> or <c>https</c> address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stream, or <see langword="null"/> when the server does not support range requests or the document is empty, so the caller should download it whole.</returns>
    /// <exception cref="ArgumentException">The address is not an <c>http</c> or <c>https</c> address.</exception>
    /// <exception cref="HttpRequestException">The server could not be reached or refused the request.</exception>
    public static async Task<HttpRangeStream?> TryOpenAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("The address must be an absolute http or https address.", nameof(address));
        }

        var client = new HttpClient();
        try
        {
            using var request = CreateRequest(address, 0, 0);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            var length = ReadTotalLength(response);
            if (length <= 0)
            {
                client.Dispose();
                return null;
            }

            return new(client, address, length);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // Nothing is written.
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        var count = (int)Math.Min(buffer.Length, Length - _position);
        if (count <= 0)
        {
            return 0;
        }

        using var request = CreateRequest(_address, _position, _position + count - 1);
        using var response = _client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        var content = CheckAnswer(response, _position);
        using var body = content.ReadAsStream();
        var read = body.ReadAtLeast(buffer[..count], count, false);
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = (int)Math.Min(buffer.Length, Length - _position);
        if (count <= 0)
        {
            return 0;
        }

        using var request = CreateRequest(_address, _position, _position + count - 1);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var content = CheckAnswer(response, _position);
        var body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            var read = await body.ReadAtLeastAsync(buffer[..count], count, false, cancellationToken).ConfigureAwait(false);
            _position += read;
            return read;
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => Length + offset,
        };

        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
        _position = target;
        return target;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("The stream is read only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("The stream is read only.");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Makes a request for part of a document.</summary>
    /// <param name="address">The document's address.</param>
    /// <param name="first">The offset of the first byte.</param>
    /// <param name="last">The offset of the last byte.</param>
    /// <returns>The request.</returns>
    private static HttpRequestMessage CreateRequest(Uri address, long first, long last)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Range = new(first, last) { Unit = RangeUnit };
        request.Headers.UserAgent.ParseAdd("HyperPDF");
        return request;
    }

    /// <summary>Reads the length of the whole document from an answer to a range request.</summary>
    /// <param name="response">The answer.</param>
    /// <returns>The length, or 0 when the server ignored the range and sent the whole document or did not say how long it is.</returns>
    private static long ReadTotalLength(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange is { Length: { } total } ? total : 0;

    /// <summary>Checks that an answer holds the bytes that were asked for.</summary>
    /// <param name="response">The answer.</param>
    /// <param name="first">The offset of the first byte asked for.</param>
    /// <returns>The content to read.</returns>
    /// <exception cref="IOException">The server did not send the range that was asked for.</exception>
    private static HttpContent CheckAnswer(HttpResponseMessage response, long first)
    {
        _ = response.EnsureSuccessStatusCode();
        return response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.From == first
            ? response.Content
            : throw new IOException(string.Create(CultureInfo.InvariantCulture, $"The server did not send the bytes that start at {first}."));
    }
}
