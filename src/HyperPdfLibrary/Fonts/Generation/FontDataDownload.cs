// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using Refit;
namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Downloads only the binary resource requested by a document.</summary>
internal static class FontDataDownload
{
    /// <summary>The maximum number of attempts within the request deadline.</summary>
    internal const int MaximumAttempts = 3;

    /// <summary>The maximum time for a resource request.</summary>
    internal const int TimeoutSeconds = 30;

    /// <summary>The initial transient failure backoff.</summary>
    private const int RetryMilliseconds = 250;

    /// <summary>Reads one pinned binary resource from a font pack.</summary>
    /// <param name="state">The selected resource cache.</param>
    /// <param name="path">The repository path.</param>
    /// <param name="cancellationToken">Cancels I/O.</param>
    /// <returns>The resource bytes.</returns>
    internal static async ValueTask<byte[]> ReadAsync(FontDataResourceState state, string path, CancellationToken cancellationToken) =>
        await ReadAsync(state.Api, path, cancellationToken).ConfigureAwait(false);

    /// <summary>Reads a resource through an owned caller's generated client with bounded transient recovery.</summary>
    /// <param name="api">The download client.</param>
    /// <param name="path">The repository path.</param>
    /// <param name="cancellationToken">Cancels the entire download and backoff.</param>
    /// <returns>The complete resource bytes.</returns>
    internal static async ValueTask<byte[]> ReadAsync(IFontDataApi api, string path, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        var attempt = 0;
        while (true)
        {
            attempt++;
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                using var response = await api.ReadAsync(path, deadline.Token).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < MaximumAttempts && !deadline.IsCancellationRequested && IsTransient(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(RetryMilliseconds * attempt), deadline.Token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Recognizes recoverable GET failures without retrying protocol or content errors.</summary>
    /// <param name="exception">The transport or status failure.</param>
    /// <returns>Whether replaying the resource GET can recover.</returns>
    internal static bool IsTransient(Exception exception) => exception switch
    {
        HttpRequestException request => IsTransientRequest(request),
        HttpIOException response => response.HttpRequestError == HttpRequestError.ResponseEnded,
        ApiRequestException or IOException when exception.InnerException is { } inner => IsTransient(inner),
        SocketException socket => IsTransientSocket(socket.SocketErrorCode),
        _ => false,
    };

    /// <summary>Separates transient HTTP status failures from permanent protocol failures.</summary>
    /// <param name="request">The HTTP failure.</param>
    /// <returns>Whether the GET may be replayed.</returns>
    private static bool IsTransientRequest(HttpRequestException request) => request.StatusCode is { } status
        ? status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
        : request.HttpRequestError switch
        {
            HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded => true,
            HttpRequestError.Unknown when request.InnerException is { } inner => IsTransient(inner),
            _ => false,
        };

    /// <summary>Limits socket recovery to interrupted or temporarily unreachable transports.</summary>
    /// <param name="error">The socket failure.</param>
    /// <returns>Whether the GET may be replayed.</returns>
    private static bool IsTransientSocket(SocketError error) => error is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.TimedOut
        or SocketError.NetworkDown or SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.TryAgain;
}
