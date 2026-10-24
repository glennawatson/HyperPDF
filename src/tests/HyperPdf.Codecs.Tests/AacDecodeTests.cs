// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>
/// Checks AAC decoding against FFmpeg's decode of the same files, kept next to each file as 24 bit FLAC. The two decoders use different
/// arithmetic, so the sound has to agree closely rather than exactly: the error has to be far below the sound.
/// </summary>
public sealed class AacDecodeTests
{
    /// <summary>The least ratio of sound to error, in decibels, a decode may have against the reference.</summary>
    private const double MinimumSignalToError = 80D;

    /// <summary>The least root mean square level the reference may have, which shows it is sound and not silence.</summary>
    private const double MinimumLevel = 0.005D;

    /// <summary>The ratio of an amplitude to a decibel, for turning an energy ratio into decibels.</summary>
    private const double DecibelsPerDecade = 10D;

    /// <summary>The samples the decoded length may differ from the reference by: the end of the last frame past the end of the sound.</summary>
    private const int EndSlack = 2_048;

    /// <summary>Checks that a file decodes to the reference sound.</summary>
    /// <param name="name">The file name with its extension.</param>
    /// <param name="reference">The reference file name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("aac-lc-stereo-44100.m4a", "aac-lc-stereo-44100.reference.flac")]
    [Arguments("aac-lc-mono-22050.m4a", "aac-lc-mono-22050.reference.flac")]
    [Arguments("aac-lc-tools-44100.m4a", "aac-lc-tools-44100.reference.flac")]
    [Arguments("aac-lc-stereo-fragmented.mp4", "aac-lc-stereo-fragmented.reference.flac")]
    [Arguments("aac-lc-noise-24k.m4a", "aac-lc-noise-24k.reference.flac")]
    [Arguments("aac-lc-stereo-44100.aac", "aac-lc-adts.reference.flac")]
    public async Task File_AgreesWithTheReference(string name, string reference)
    {
        var decoded = Decode(name);
        var expected = Decode(reference);

        await Assert.That(Math.Abs(decoded.Count - expected.Count)).IsLessThanOrEqualTo(EndSlack);
        await Assert.That(RootMeanSquare(expected.Samples)).IsGreaterThan(MinimumLevel);
        await Assert.That(SignalToError(decoded.Samples, expected.Samples)).IsGreaterThanOrEqualTo(MinimumSignalToError);
    }

    /// <summary>Finds the root mean square of a sound.</summary>
    /// <param name="samples">The sound.</param>
    /// <returns>The level, with full scale being 1.</returns>
    private static double RootMeanSquare(ReadOnlySpan<float> samples)
    {
        double energy = 0;
        foreach (var sample in samples)
        {
            energy += sample * sample;
        }

        return Math.Sqrt(energy / samples.Length);
    }

    /// <summary>Decodes a whole file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>All the sound.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static PcmBuffer Decode(string name)
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))
            ?? throw new InvalidDataException($"{name} did not open.");
        var index = container.FindTrack(MediaTrackKind.Audio);
        using var decoder = AudioDecoders.Create(container.Tracks[index])!;
        var packet = new MediaPacket();
        var pcm = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, pcm);
        }

        return pcm;
    }

    /// <summary>Works out how far above the error the reference sound is, over the samples both sounds have.</summary>
    /// <param name="decoded">The decoded sound.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>The ratio of the sound's energy to the error's energy, in decibels.</returns>
    private static double SignalToError(ReadOnlySpan<float> decoded, ReadOnlySpan<float> reference)
    {
        var count = Math.Min(decoded.Length, reference.Length);
        double signal = 0;
        double error = 0;
        for (var index = 0; index < count; index++)
        {
            var difference = decoded[index] - reference[index];
            signal += reference[index] * reference[index];
            error += difference * difference;
        }

        return error == 0 ? double.PositiveInfinity : DecibelsPerDecade * Math.Log10(signal / error);
    }
}
