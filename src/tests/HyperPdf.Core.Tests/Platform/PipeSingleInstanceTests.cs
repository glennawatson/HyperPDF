// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests for <see cref="PipeSingleInstance"/>, the single window used on Windows and macOS; named pipes also work on Linux.</summary>
public sealed class PipeSingleInstanceTests
{
    /// <summary>The length of a name too long to fit in a socket path as it is.</summary>
    private const int LongName = 200;

    /// <summary>The longest socket path macOS accepts.</summary>
    private const int MaxSocketPath = 104;

    /// <summary>The time limit for the forwarding test in milliseconds.</summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>How long to wait for a forwarded request. A loaded arm64 runner can take far longer than a quiet machine.</summary>
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>A second launch hands its documents to the first and cannot claim the window itself.</summary>
    /// <param name="cancellationToken">Cancellation when the test exceeds its time limit.</param>
    /// <returns>A task.</returns>
    [Test]
    [Timeout(TimeoutMilliseconds)]
    public async Task ForwardsToTheRunningInstance(CancellationToken cancellationToken)
    {
        var name = $"HyperPdfTest-{Guid.NewGuid():N}";
        using var first = PipeSingleInstance.TryStart(name);
        var received = new TaskCompletionSource<OpenRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = first!.OpenRequests.SubscribeSafe(request => received.TrySetResult(request), static _ => { });

        var second = PipeSingleInstance.TryStart(name);
        var forwarded = await PipeSingleInstance.TryForwardAsync(name, new(["documents/a.pdf", "https://example.com/b.pdf"], "token"));
        var request = await received.Task.WaitAsync(Wait, cancellationToken);

        await Assert.That(second).IsNull();
        await Assert.That(forwarded).IsTrue();
        await Assert.That(request.Uris).IsEquivalentTo(["documents/a.pdf", "https://example.com/b.pdf"]);
        await Assert.That(request.ActivationToken).IsEqualTo("token");
    }

    /// <summary>With nothing running, forwarding fails quickly so the launch opens its own window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsWhenNothingIsRunning() =>
        await Assert.That(await PipeSingleInstance.TryForwardAsync($"HyperPdfTest-{Guid.NewGuid():N}", new([], null))).IsFalse();

    /// <summary>A long name still makes a socket path macOS accepts, and the same name always maps to the same pipe.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsSocketPathsShort()
    {
        var name = new string('n', LongName);
        var pipe = PipeSingleInstance.PipeNameFor(name);

        await Assert.That(pipe).IsEqualTo(PipeSingleInstance.PipeNameFor(name));
        await Assert.That(PipeSingleInstance.PipeNameFor("App-me")).IsEqualTo("App-me");
        if (!OperatingSystem.IsWindows())
        {
            await Assert.That(Path.GetTempPath().Length + "CoreFxPipe_".Length + pipe.Length).IsLessThanOrEqualTo(MaxSocketPath);
        }
    }

    /// <summary>Messages round trip, dropping anything with a line break and an empty token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncodesRequests()
    {
        var decoded = PipeSingleInstance.Decode(PipeSingleInstance.Encode(new(["a.pdf", "bad\nname.pdf", "c d.pdf"], null)));

        await Assert.That(decoded.Uris).IsEquivalentTo(["a.pdf", "c d.pdf"]);
        await Assert.That(decoded.ActivationToken).IsNull();
        await Assert.That(PipeSingleInstance.NameFor("App")).StartsWith("App-");
    }
}
