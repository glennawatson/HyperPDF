// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Exposes partial writes, body faults and disposal deterministically.</summary>
/// <param name="failure">The fault after the prefix, or null for success.</param>
/// <param name="awaitCancellation">Whether the body remains active until cancelled.</param>
internal sealed class FontDownloadContent(Exception? failure, bool awaitCancellation) : HttpContent
{
    /// <summary>The first bytes before a body interruption.</summary>
    private static readonly byte[] Prefix = [1];

    /// <summary>The remaining successful bytes.</summary>
    private static readonly byte[] Suffix = [0];

    /// <summary>Signals that the response prefix was written.</summary>
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes when the body has written its prefix.</summary>
    internal Task Started => _started.Task;

    /// <summary>Gets a value indicating whether the response content was disposed.</summary>
    internal bool IsDisposed { get; private set; }

    /// <summary>Copies the scripted body.</summary>
    /// <param name="stream">The destination buffer.</param>
    /// <param name="context">The transport context.</param>
    /// <returns>The copy operation.</returns>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);

    /// <summary>Writes a prefix before failing, completing or observing cancellation.</summary>
    /// <param name="stream">The destination buffer.</param>
    /// <param name="context">The transport context.</param>
    /// <param name="cancellationToken">Cancels the active response read.</param>
    /// <returns>The copy operation.</returns>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Prefix, cancellationToken);
        _ = _started.TrySetResult();
        if (awaitCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (failure is not null)
        {
            throw failure;
        }

        await stream.WriteAsync(Suffix, cancellationToken);
    }

    /// <summary>Leaves length unknown so the whole body is read.</summary>
    /// <param name="length">Receives zero.</param>
    /// <returns>False.</returns>
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    /// <summary>Records owned response cleanup.</summary>
    /// <param name="disposing">Whether managed state is being disposed.</param>
    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
