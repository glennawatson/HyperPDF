// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Returns queued transport faults or owned responses without network access.</summary>
/// <param name="replies">The successive outcomes.</param>
internal sealed class FontDownloadHandler(params object[] replies) : HttpMessageHandler
{
    /// <summary>The last response whose ownership must end before another attempt.</summary>
    private FontDownloadContent? _previous;

    /// <summary>Gets the number of GET attempts.</summary>
    internal int Attempts { get; private set; }

    /// <summary>Returns the next deterministic transport outcome.</summary>
    /// <param name="request">The generated GET.</param>
    /// <param name="cancellationToken">Cancels transport.</param>
    /// <returns>The response or fault.</returns>
    /// <exception cref="InvalidOperationException">The previous response has not been disposed.</exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_previous is { IsDisposed: false })
        {
            throw new InvalidOperationException("The previous response remains owned at the next attempt.");
        }

        var reply = replies[Attempts];
        Attempts++;
        _previous = (reply as HttpResponseMessage)?.Content as FontDownloadContent;
        return reply is Exception error ? Task.FromException<HttpResponseMessage>(error) : Task.FromResult((HttpResponseMessage)reply);
    }
}
