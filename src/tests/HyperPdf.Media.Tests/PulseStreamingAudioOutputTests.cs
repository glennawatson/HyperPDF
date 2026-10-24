// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Media;
using HyperPdf.Platform.Linux.Audio;
using ReactiveUI.Primitives;

namespace HyperPdf.Media.Tests;

/// <summary>Tests for <see cref="PulseStreamingAudioOutput"/> against the sound server of the computer. They write silence, so nothing is heard, and skip when there is no sound server.</summary>
public sealed class PulseStreamingAudioOutputTests
{
    /// <summary>Why the tests skip off Linux.</summary>
    private const string NotLinux = "PulseAudio is a Linux sound server.";

    /// <summary>Why the tests skip without a sound server.</summary>
    private const string NoServer = "No PulseAudio or PipeWire sound server accepted a stream.";

    /// <summary>The samples each second of the test stream.</summary>
    private const int Rate = 48_000;

    /// <summary>The channels of the test stream.</summary>
    private const int Channels = 2;

    /// <summary>The seconds of silence written.</summary>
    private const double Seconds = 1.0;

    /// <summary>The least the write may take: the server holds about a fifth of a second, so the rest has to be waited for.</summary>
    private const double LeastSeconds = 0.6;

    /// <summary>The fewest pictures the 30 picture test clip shows when few are dropped.</summary>
    private const int FewestPictures = 24;

    /// <summary>How long to wait for the clip to end, in seconds.</summary>
    private const int Wait = 30;

    /// <summary>The most latency the output may report after a write, in seconds.</summary>
    private const double MostLatency = 0.5;

    /// <summary>Verifies a write waits for the server, the latency stays under half a second, and flushing and closing leave none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesWaitForTheServerAndLatencyIsReported()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test(NotLinux);
        }

        using var output = new PulseStreamingAudioOutput();
        if (!output.IsAvailable || !output.Start(Rate, Channels))
        {
            Skip.Test(NoServer);
        }

        var silence = new float[(int)(Rate * Channels * Seconds)];
        var started = Stopwatch.GetTimestamp();
        var accepted = await Task.Run(() => output.Write(silence, CancellationToken.None));
        var took = Stopwatch.GetElapsedTime(started).TotalSeconds;

        await Assert.That(accepted).IsTrue();
        await Assert.That(took).IsGreaterThan(LeastSeconds);
        await Assert.That(output.Latency.TotalSeconds).IsGreaterThan(0).And.IsLessThan(MostLatency);
        output.Flush();
        output.Close();
        await Assert.That(output.Latency).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>Verifies a clip with sound and a picture plays to its end through the real sound server, with the pictures shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysAClipThroughTheRealOutput()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test(NotLinux);
        }

        using var probe = new PulseStreamingAudioOutput();
        if (!probe.IsAvailable || !probe.Start(Rate, Channels))
        {
            Skip.Test(NoServer);
        }

        probe.Close();
        using var player = new ManagedMediaPlayer(static () => new PulseStreamingAudioOutput(), false);
        _ = await player.OpenAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "av-h264-aac-text.mp4"), CancellationToken.None);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State is MediaPlayerState.Ended or MediaPlayerState.Failed)
                {
                    _ = ended.TrySetResult();
                }
            },
            static _ => { });

        player.Play();
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(Wait));

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestPictures);
        await Assert.That(player.Problem).IsNull();
    }

    /// <summary>Verifies a cancelled write stops at once and writing to a closed output fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledWriteStopsAndClosedOutputRefuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test(NotLinux);
        }

        using var output = new PulseStreamingAudioOutput();
        if (!output.IsAvailable || !output.Start(Rate, Channels))
        {
            Skip.Test(NoServer);
        }

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var silence = new float[Rate * Channels];

        await Assert.That(output.Write(silence, cancelled.Token)).IsFalse();
        output.Close();
        await Assert.That(output.Write(silence, CancellationToken.None)).IsFalse();
    }
}
