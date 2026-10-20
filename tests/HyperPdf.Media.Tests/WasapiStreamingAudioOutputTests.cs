// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.Versioning;
using HyperPdf.Platform.Windows.Audio;

namespace HyperPdf.Media.Tests;

/// <summary>Tests for <see cref="WasapiStreamingAudioOutput"/> against the sound device of the computer. They write silence, so nothing is heard.</summary>
public sealed class WasapiStreamingAudioOutputTests
{
    /// <summary>Why the tests skip off Windows.</summary>
    private const string NotWindows = "WASAPI is the Windows sound system.";

    /// <summary>The samples each second of the test stream.</summary>
    private const int Rate = 48_000;

    /// <summary>The channels of the test stream.</summary>
    private const int Channels = 2;

    /// <summary>The seconds of silence written.</summary>
    private const double Seconds = 1.0;

    /// <summary>The least the write may take: the device holds a fraction of a second, so the rest has to be waited for.</summary>
    private const double LeastSeconds = 0.4;

    /// <summary>Verifies a computer with no sound device refuses to start without an exception, and one with a device paces writes and reports latency.</summary>
    /// <returns>A task.</returns>
    [Test]
    [SupportedOSPlatform("windows")]
    public async Task StartsOrRefusesCleanlyAndPacesWrites()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(NotWindows);
        }

        using var output = new WasapiStreamingAudioOutput();
        var silence = new float[(int)(Rate * Channels * Seconds)];
        if (!output.IsAvailable || !output.Start(Rate, Channels))
        {
            await Assert.That(output.Write(silence, CancellationToken.None)).IsFalse();
            await Assert.That(output.Latency).IsEqualTo(TimeSpan.Zero);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var accepted = await Task.Run(() => output.Write(silence, CancellationToken.None));
        var took = Stopwatch.GetElapsedTime(started).TotalSeconds;

        await Assert.That(accepted).IsTrue();
        await Assert.That(took).IsGreaterThan(LeastSeconds);
        output.Flush();
        output.Close();
        await Assert.That(output.Latency).IsEqualTo(TimeSpan.Zero);
    }
}
