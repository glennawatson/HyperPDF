// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Speech;
using HyperPdf.Platform.Linux.Audio;
using HyperPdf.Platform.MacOS.Audio;
using HyperPdf.Platform.Windows.Audio;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>
/// Plays through the platform's real audio output: PulseAudio or PipeWire on Linux (CI starts a silent sink), WASAPI on
/// Windows and Audio Queue Services on macOS; skipped when the machine has no output device.
/// </summary>
[NotInParallel]
public sealed class AudioOutputTests
{
    /// <summary>Kokoro's sample rate.</summary>
    private const int Hertz = 24_000;

    /// <summary>The least share of its length a clip must take to play.</summary>
    private const double MinimumShare = 0.8;

    /// <summary>Divides a sample rate into a tenth of a second.</summary>
    private const int Tenths = 10;

    /// <summary>How long the clip plays, in seconds.</summary>
    private const double ClipSeconds = 0.5;

    /// <summary>How long the long clip plays, in seconds.</summary>
    private const int LongSeconds = 4;

    /// <summary>When the long clip is stopped.</summary>
    private static readonly TimeSpan StopAfter = TimeSpan.FromMilliseconds(300);

    /// <summary>The longest stopping may take.</summary>
    private static readonly TimeSpan StopWithin = TimeSpan.FromMilliseconds(400);

    /// <summary>The most a clip may overrun its length, for the server's buffering.</summary>
    private static readonly TimeSpan Overrun = TimeSpan.FromSeconds(1);

    /// <summary>Playing completes once the audio has been heard, not before and not much after.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysForTheClipsLength()
    {
        using var output = await RequireServerAsync();
        var clip = new SpeechAudio(Tone(ClipSeconds), Hertz);
        var watch = Stopwatch.GetTimestamp();

        await output.PlayAsync(clip, CancellationToken.None);

        await Assert.That(Stopwatch.GetElapsedTime(watch)).IsGreaterThan(clip.Duration * MinimumShare);
        await Assert.That(Stopwatch.GetElapsedTime(watch)).IsLessThan(clip.Duration + Overrun);
    }

    /// <summary>Stopping takes effect at once, discarding what is buffered, and the output plays again afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopsAtOnceAndPlaysAgain()
    {
        using var output = await RequireServerAsync();
        using var cancellation = new CancellationTokenSource(StopAfter);
        var watch = Stopwatch.GetTimestamp();

        await output.PlayAsync(new(Tone(LongSeconds), Hertz), cancellation.Token);

        await Assert.That(Stopwatch.GetElapsedTime(watch)).IsLessThan(StopAfter + StopWithin);
        var again = Stopwatch.GetTimestamp();
        await output.PlayAsync(new(Tone(ClipSeconds), Hertz), CancellationToken.None);
        await Assert.That(Stopwatch.GetElapsedTime(again)).IsLessThan(TimeSpan.FromSeconds(ClipSeconds) + Overrun);
    }

    /// <summary>A change of sample rate reopens the stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsTheSampleRate()
    {
        using var output = await RequireServerAsync();
        const int OtherRate = 16_000;

        await output.PlayAsync(new(new float[OtherRate / Tenths], OtherRate), CancellationToken.None);
        await output.PlayAsync(new(new float[Hertz / Tenths], Hertz), CancellationToken.None);

        await Assert.That(output.IsAvailable).IsTrue();
    }

    /// <summary>Makes a quiet tone.</summary>
    /// <param name="seconds">How long.</param>
    /// <returns>The samples.</returns>
    private static float[] Tone(double seconds)
    {
        const float Quiet = 0.05F;
        const double Pitch = 440;
        var samples = new float[(int)(seconds * Hertz)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = Quiet * (float)Math.Sin(Math.Tau * Pitch * i / Hertz);
        }

        return samples;
    }

    /// <summary>Creates the output, skipping the test when there is no sound server.</summary>
    /// <returns>The output.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">There is no sound server.</exception>
    private static async Task<IAudioOutput> RequireServerAsync()
    {
#if NET11_0_OR_GREATER
        await Task.CompletedTask;

        // The .NET 10 and .NET 11 test runs execute at the same time; one of them playing keeps the timings meaningful.
        throw new TUnit.Core.Exceptions.SkipTestException("Playback is tested in the .NET 10 run.");
#else
        var output = CreateOutput();
        if (output.IsAvailable)
        {
            // Read Aloud keeps one stream open across sentences; a silent sink takes a moment to start a new one, so the
            // timings are measured on a warm stream.
            await output.PlayAsync(new(new float[Hertz / Tenths], Hertz), CancellationToken.None);
            return output;
        }

        output.Dispose();
        throw new TUnit.Core.Exceptions.SkipTestException("This machine has no audio output device.");
#endif
    }

#if !NET11_0_OR_GREATER
    /// <summary>Creates the output the app uses on this platform.</summary>
    /// <returns>The output.</returns>
    private static IAudioOutput CreateOutput()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WasapiAudioOutput();
        }

        return OperatingSystem.IsMacOS() ? new AudioQueueOutput() : new PulseAudioOutput();
    }
#endif
}
