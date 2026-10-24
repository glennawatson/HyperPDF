// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Audio;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks the channel mixer and the resampler that turn decoded sound into stereo at the output rate.</summary>
public sealed class AudioResamplerTests
{
    /// <summary>The output rate of the tests.</summary>
    private const int OutputRate = 48_000;

    /// <summary>The rate of CD sound.</summary>
    private const int CdRate = 44_100;

    /// <summary>The rate of high resolution sound.</summary>
    private const int HighRate = 96_000;

    /// <summary>The frequency of the test tone, in Hz.</summary>
    private const double ToneHertz = 1_000;

    /// <summary>A tone above half of the output rate, which the filter must remove.</summary>
    private const double HighToneHertz = 30_000;

    /// <summary>The seconds of sound in the long tests.</summary>
    private const int Seconds = 2;

    /// <summary>The frames in each call when sound arrives in chunks.</summary>
    private const int ChunkFrames = 333;

    /// <summary>The number of channels of stereo sound.</summary>
    private const int Stereo = 2;

    /// <summary>The number of channels of 5.1 sound.</summary>
    private const int SixChannels = 6;

    /// <summary>The level of the test tone.</summary>
    private const float Level = 0.5F;

    /// <summary>The share of the tone's level that must come through the pass band, as a fraction.</summary>
    private const double PassBandAccuracy = 0.01;

    /// <summary>The most level, as a fraction of the input, that may be left of a tone in the stop band.</summary>
    private const double StopBandLeak = 0.003;

    /// <summary>The frames at each end of the output that the test skips, where the filter is settling.</summary>
    private const int SettleFrames = 200;

    /// <summary>The weight of a channel shared between both sides.</summary>
    private const float Shared = 0.70710678F;

    /// <summary>The most two samples may differ and still count as the same.</summary>
    private const float Tolerance = 1e-6F;

    /// <summary>The tolerance for the mix of channels, which adds a few products.</summary>
    private const float MixTolerance = 1e-5F;

    /// <summary>The index of the centre channel of 5.1 sound.</summary>
    private const int ThirdChannel = 2;

    /// <summary>The index of the left surround channel of 5.1 sound.</summary>
    private const int FifthChannel = 4;

    /// <summary>The index of the right surround channel of 5.1 sound.</summary>
    private const int SixthChannel = 5;

    /// <summary>The part of a second the same-rate test plays.</summary>
    private const int Tenth = 10;

    /// <summary>One frame of 5.1 sound: left, right, centre, low frequency, left surround, right surround.</summary>
    private static readonly float[] SixChannelFrame = [0.1F, 0.2F, 0.4F, 0.9F, 0.3F, 0.5F];

    /// <summary>Two frames of mono sound.</summary>
    private static readonly float[] MonoFrames = [0.25F, -0.5F];

    /// <summary>The same two frames as stereo.</summary>
    private static readonly float[] MonoAsStereo = [0.25F, 0.25F, -0.5F, -0.5F];

    /// <summary>Verifies that stereo sound at the output rate passes through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SameRateStereo_PassesThrough()
    {
        var input = Tone(OutputRate, Stereo, ToneHertz, OutputRate / Tenth);
        var resampler = new AudioResampler(OutputRate, Stereo, OutputRate);
        var output = new PcmBuffer();

        resampler.Process(input, output);

        await Assert.That(output.Samples.ToArray()).IsEquivalentTo(input);
    }

    /// <summary>Verifies that mono sound is played from both sides.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Mono_IsPlayedFromBothSides()
    {
        var resampler = new AudioResampler(OutputRate, 1, OutputRate);
        var output = new PcmBuffer();

        resampler.Process(MonoFrames, output);

        await Assert.That(output.Samples.ToArray()).IsEquivalentTo(MonoAsStereo);
    }

    /// <summary>Verifies that the centre and the surrounds of 5.1 sound are mixed in at -3 dB and the low frequency channel is left out.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SixChannels_MixCentreAndSurroundsAtMinusThreeDecibels()
    {
        var left = new float[1];
        var right = new float[1];

        ChannelMixer.Mix(SixChannelFrame, SixChannels, left, right);

        await Assert.That(left[0]).IsEqualTo(SixChannelFrame[0] + (SixChannelFrame[ThirdChannel] * Shared) + (SixChannelFrame[FifthChannel] * Shared)).Within(MixTolerance);
        await Assert.That(right[0]).IsEqualTo(SixChannelFrame[1] + (SixChannelFrame[ThirdChannel] * Shared) + (SixChannelFrame[SixthChannel] * Shared)).Within(MixTolerance);
    }

    /// <summary>Verifies that a tone keeps its pitch, its level and its length when 44.1 kHz becomes 48 kHz.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CdToOutputRate_KeepsToneAndLength()
    {
        var output = Convert(CdRate, Stereo, ToneHertz, Seconds);

        await Assert.That(output.Length / Stereo).IsBetween((Seconds * OutputRate) - SettleFrames, Seconds * OutputRate);
        await Assert.That(PeakOfSettled(output)).IsEqualTo(Level).Within(Level * PassBandAccuracy);
        await Assert.That(Crossings(output)).IsEqualTo((int)(ToneHertz * Seconds * Stereo)).Within(Stereo);
    }

    /// <summary>Verifies that a tone above the output's half rate is removed when 96 kHz becomes 48 kHz.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Downsampling_RemovesWhatTheOutputCannotHold()
    {
        var output = Convert(HighRate, Stereo, HighToneHertz, 1);

        await Assert.That(PeakOfSettled(output)).IsLessThan(Level * StopBandLeak);
    }

    /// <summary>Verifies that sound arriving in small chunks gives the same result as sound arriving at once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Chunks_GiveTheSameSoundAsOneCall()
    {
        var input = Tone(CdRate, Stereo, ToneHertz, CdRate);
        var whole = new PcmBuffer();
        new AudioResampler(CdRate, Stereo, OutputRate).Process(input, whole);
        var parts = new PcmBuffer();
        var resampler = new AudioResampler(CdRate, Stereo, OutputRate);
        for (var offset = 0; offset < input.Length; offset += ChunkFrames * Stereo)
        {
            resampler.Process(input.AsSpan(offset, Math.Min(ChunkFrames * Stereo, input.Length - offset)), parts);
        }

        var shared = Math.Min(whole.Count, parts.Count);
        var worst = 0F;
        for (var index = 0; index < shared; index++)
        {
            worst = Math.Max(worst, Math.Abs(whole.Samples[index] - parts.Samples[index]));
        }

        await Assert.That(shared).IsGreaterThan(OutputRate);
        await Assert.That(worst).IsLessThan(Tolerance);
    }

    /// <summary>Makes interleaved sound with the same sine tone in every channel.</summary>
    /// <param name="rate">The samples a second.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="hertz">The tone's frequency.</param>
    /// <param name="frames">The number of frames.</param>
    /// <returns>The samples.</returns>
    private static float[] Tone(int rate, int channels, double hertz, int frames)
    {
        var samples = new float[frames * channels];
        for (var frame = 0; frame < frames; frame++)
        {
            var value = (float)(Level * Math.Sin(Math.Tau * hertz * frame / rate));
            for (var channel = 0; channel < channels; channel++)
            {
                samples[(frame * channels) + channel] = value;
            }
        }

        return samples;
    }

    /// <summary>Makes a tone, converts it and returns the result.</summary>
    /// <param name="rate">The input rate.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="hertz">The tone's frequency.</param>
    /// <param name="seconds">The seconds of sound.</param>
    /// <returns>The converted samples.</returns>
    private static float[] Convert(int rate, int channels, double hertz, int seconds)
    {
        var output = new PcmBuffer();
        new AudioResampler(rate, channels, OutputRate).Process(Tone(rate, channels, hertz, rate * seconds), output);
        return output.Samples.ToArray();
    }

    /// <summary>Finds the highest level in the part of the sound away from both ends.</summary>
    /// <param name="samples">The interleaved stereo samples.</param>
    /// <returns>The highest absolute value.</returns>
    private static double PeakOfSettled(float[] samples)
    {
        var peak = 0D;
        for (var index = SettleFrames * Stereo; index < samples.Length - (SettleFrames * Stereo); index++)
        {
            peak = Math.Max(peak, Math.Abs(samples[index]));
        }

        return peak;
    }

    /// <summary>Counts the times the left channel crosses zero.</summary>
    /// <param name="samples">The interleaved stereo samples.</param>
    /// <returns>The crossings.</returns>
    private static int Crossings(float[] samples)
    {
        var crossings = 0;
        for (var index = Stereo; index < samples.Length; index += Stereo)
        {
            if ((samples[index - Stereo] < 0) != (samples[index] < 0))
            {
                crossings++;
            }
        }

        return crossings;
    }
}
