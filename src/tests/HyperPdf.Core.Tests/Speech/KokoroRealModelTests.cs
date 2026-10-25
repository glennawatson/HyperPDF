// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Speech;
using HyperPdf.Speech.Kokoro;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>
/// Runs the real Kokoro-82M model: the audio must be speech-like (not silent, not clipped, plausibly long for the
/// words), synthesis must keep ahead of playback, the result must be repeatable, and long or awkward text must work.
/// </summary>
[NotInParallel(nameof(KokoroRealModelTests))]
public sealed class KokoroRealModelTests : IDisposable
{
    /// <summary>Kokoro's sample rate.</summary>
    private const int Hertz = 24_000;

    /// <summary>A sentence of nine words.</summary>
    private const string Sentence = "The quick brown fox jumps over the lazy dog today.";

    /// <summary>The fewest seconds per word of natural speech.</summary>
    private const double MinSecondsPerWord = 0.15;

    /// <summary>The most seconds per word of natural speech, with pauses.</summary>
    private const double MaxSecondsPerWord = 0.8;

    /// <summary>The words in <see cref="Sentence"/>.</summary>
    private const int SentenceWords = 10;

    /// <summary>The quietest root mean square level counted as speech.</summary>
    private const double MinLevel = 0.01;

    /// <summary>The loudest sample allowed, a little headroom over full scale.</summary>
    private const float MaxPeak = 1.05F;

    /// <summary>The share of samples that may sit at the peak before audio counts as clipped.</summary>
    private const double MaxClippedShare = 0.001;

    /// <summary>The slowest synthesis allowed, as a multiple of the audio's length: faster than playback, so no gaps.</summary>
    private const double MaxRealTimeFactor = 1;

    /// <summary>The readings timed; the fastest counts.</summary>
    private const int TimingAttempts = 3;

    /// <summary>The fastest speed.</summary>
    private const float Fast = 1.5F;

    /// <summary>How much shorter fast speech must be.</summary>
    private const double FastShare = 0.85;

    /// <summary>The paragraph repeated to exceed one model window.</summary>
    private const int LongRepeats = 12;

    /// <summary>The fewest seconds each repeat of the long paragraph takes.</summary>
    private const double MinSecondsPerRepeat = 3;

    /// <summary>The longest silence allowed inside speech.</summary>
    private const double MaxSilenceSeconds = 1.5;

    /// <summary>The American voice.</summary>
    private const string Heart = "af_heart";

    /// <summary>The hold on the voices, so no other test process synthesizes while this test does.</summary>
    private IDisposable? _voices;

    /// <summary>Waits until no other test process is synthesizing, then holds the voices for this test.</summary>
    /// <returns>A task.</returns>
    [Before(Test)]
    public async Task HoldVoicesAsync() => _voices = await RealTimeGate.EnterAsync();

    /// <inheritdoc/>
    public void Dispose() => _voices?.Dispose();

    /// <summary>A sentence sounds like speech: long enough for its words, audible and not clipped or broken.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpeaksASentence()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);

        var audio = await engine.SynthesizeAsync(Sentence, Heart, 1, CancellationToken.None);

        await Assert.That(engine.IsReady).IsTrue();
        await Assert.That(audio.SampleRate).IsEqualTo(Hertz);
        await Assert.That(audio.Duration.TotalSeconds).IsBetween(SentenceWords * MinSecondsPerWord, SentenceWords * MaxSecondsPerWord);
        await AssertSpeechLike(audio);
    }

    /// <summary>Synthesis keeps ahead of playback once the model is loaded, so sentences follow without gaps.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsAheadOfPlayback()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);
        _ = await engine.SynthesizeAsync("Warm up.", Heart, 1, CancellationToken.None);

        // The best of a few readings: the engine's speed, not a moment of contention.
        var best = double.MaxValue;
        for (var attempt = 0; attempt < TimingAttempts; attempt++)
        {
            var start = Stopwatch.GetTimestamp();
            var audio = await engine.SynthesizeAsync(Sentence, Heart, 1, CancellationToken.None);
            best = Math.Min(best, Stopwatch.GetElapsedTime(start).TotalSeconds / audio.Duration.TotalSeconds);
        }

        await Assert.That(best).IsLessThan(MaxRealTimeFactor);
    }

    /// <summary>The same text gives the same audio, and faster speech is shorter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsRepeatableAndFollowsSpeed()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);

        var first = await engine.SynthesizeAsync(Sentence, Heart, 1, CancellationToken.None);
        var second = await engine.SynthesizeAsync(Sentence, Heart, 1, CancellationToken.None);
        var fast = await engine.SynthesizeAsync(Sentence, Heart, Fast, CancellationToken.None);

        await Assert.That(second.Samples.Length).IsEqualTo(first.Samples.Length);
        await Assert.That(fast.Samples.Length).IsLessThan((int)(first.Samples.Length * FastShare));
    }

    /// <summary>Every offered voice speaks, British voices included.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryVoiceSpeaks()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);

        foreach (var voice in KokoroModel.Voices)
        {
            var audio = await engine.SynthesizeAsync("Reading aloud should feel calm.", voice.Id, 1, CancellationToken.None);
            await AssertSpeechLike(audio);
        }
    }

    /// <summary>Text longer than one model window, with numbers, abbreviations and symbols, is spoken whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpeaksLongAndAwkwardText()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);
        var paragraph = string.Concat(Enumerable.Repeat("Dr. Smith measured 3.5% growth in 1984, & the PDF reports agree. ", LongRepeats));

        var audio = await engine.SynthesizeAsync(paragraph, Heart, 1, CancellationToken.None);

        await Assert.That(audio.Duration.TotalSeconds).IsGreaterThan(LongRepeats * MinSecondsPerRepeat);
        await AssertSpeechLike(audio);
    }

    /// <summary>Text with nothing to say gives silence rather than failing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesNothingToSay()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);

        var audio = await engine.SynthesizeAsync("— … —", Heart, 1, CancellationToken.None);

        await Assert.That(audio.Duration.TotalSeconds).IsLessThan(1);
    }

    /// <summary>Checks audio is audible, finite, not clipped and has no long silent gap in the middle.</summary>
    /// <param name="audio">The audio.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSpeechLike(SpeechAudio audio)
    {
        double sum = 0;
        float peak = 0;
        var clipped = 0;
        foreach (var sample in audio.Samples)
        {
            sum += sample * (double)sample;
            peak = Math.Max(peak, Math.Abs(sample));
            clipped += Math.Abs(sample) >= 1 ? 1 : 0;
        }

        var level = Math.Sqrt(sum / Math.Max(1, audio.Samples.Length));
        await Assert.That(float.IsFinite(peak)).IsTrue();
        await Assert.That(level).IsGreaterThan(MinLevel);
        await Assert.That(peak).IsLessThan(MaxPeak);
        await Assert.That((double)clipped / audio.Samples.Length).IsLessThan(MaxClippedShare);
        await Assert.That(LongestSilence(audio)).IsLessThan(TimeSpan.FromSeconds(MaxSilenceSeconds));
    }

    /// <summary>Finds the longest stretch of near silence between the first and last sound.</summary>
    /// <param name="audio">The audio.</param>
    /// <returns>Its length.</returns>
    private static TimeSpan LongestSilence(SpeechAudio audio)
    {
        const float Quiet = 0.005F;
        const int Window = 240;
        var longest = 0;
        var current = 0;
        var heard = false;
        for (var start = 0; start + Window <= audio.Samples.Length; start += Window)
        {
            var loud = false;
            for (var i = start; i < start + Window && !loud; i++)
            {
                loud = Math.Abs(audio.Samples[i]) > Quiet;
            }

            if (loud)
            {
                longest = heard ? Math.Max(longest, current) : longest;
                heard = true;
                current = 0;
            }
            else
            {
                current += Window;
            }
        }

        return TimeSpan.FromSeconds((double)longest / audio.SampleRate);
    }
}
