// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>A transport that counts every request and fails it, so a test proves the code under test made none.</summary>
internal sealed class NoNetworkHandler : HttpMessageHandler
{
    /// <summary>The number of requests seen.</summary>
    private int _requests;

    /// <summary>Gets the number of requests the code under test sent.</summary>
    internal int Requests => Volatile.Read(ref _requests);

    /// <summary>Counts the request and fails it.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always: the test forbids network access.</exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _requests);
        throw new InvalidOperationException($"The test forbids network access: {request.RequestUri}");
    }
}
