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
        // A locked down machine, such as a build sandbox, may refuse a named mutex, which the single window is claimed with.
        if (!CanNameAMutex())
        {
            Skip.Test("This machine does not allow a named mutex, which the single window is claimed with.");
        }

        var name = $"HyperPdfTest-{Guid.NewGuid():N}";
        using var first = PipeSingleInstance.TryStart(name);
        await Assert.That(first).IsNotNull();

        // Waiting starts before anything is forwarded, because the requests are only given to whoever is already listening.
        var received = first!.OpenRequests.Take(1).Timeout(Wait).ToTask(cancellationToken);

        var second = PipeSingleInstance.TryStart(name);
        var forwarded = await PipeSingleInstance.TryForward(name, new(["documents/a.pdf", "https://example.com/b.pdf"], "token")).ToTask(cancellationToken);
        var request = await received;

        await Assert.That(second).IsNull();
        await Assert.That(forwarded).IsTrue();
        await Assert.That(request.Uris).IsEquivalentTo(["documents/a.pdf", "https://example.com/b.pdf"]);
        await Assert.That(request.ActivationToken).IsEqualTo("token");
    }

    /// <summary>With nothing running, forwarding fails quickly so the launch opens its own window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsWhenNothingIsRunning() =>
        await Assert.That(await PipeSingleInstance.TryForward($"HyperPdfTest-{Guid.NewGuid():N}", new([], null)).ToTask()).IsFalse();

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

    /// <summary>Tells whether this machine lets a process make a named mutex.</summary>
    /// <returns><see langword="false"/> when the operating system refuses one, as a sandbox with no shared memory does.</returns>
    private static bool CanNameAMutex()
    {
        try
        {
            using var probe = new Mutex(false, $"HyperPdfProbe-{Guid.NewGuid():N}");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
