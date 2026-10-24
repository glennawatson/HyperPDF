// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>
/// Checks Opus decoding against FFmpeg's decode of the same files, kept next to each file as 16 bit FLAC. The two decoders use different
/// arithmetic, so the sound has to agree closely rather than exactly: the error has to be far below the sound.
/// </summary>
public sealed class OpusDecodeTests
{
    /// <summary>The least ratio of sound to error, in decibels, a decode may have against the reference.</summary>
    private const double MinimumSignalToError = 90D;

    /// <summary>The least root mean square level the reference may have, which shows it is sound and not silence.</summary>
    private const double MinimumLevel = 0.01D;

    /// <summary>The ratio of an amplitude to a decibel, for turning an energy ratio into decibels.</summary>
    private const double DecibelsPerDecade = 10D;

    /// <summary>The samples the decoded length may differ from the reference by: the end of the last frame past the end of the sound.</summary>
    private const int EndSlack = 2_880;

    /// <summary>Checks that a file decodes to the reference sound.</summary>
    /// <param name="name">The file name without its extension.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("opus-mono-voip")]
    [Arguments("silk-mono-nb-20ms")]
    [Arguments("silk-mono-mb-10ms")]
    [Arguments("silk-mono-wb-20ms")]
    [Arguments("silk-mono-wb-60ms")]
    [Arguments("silk-stereo-wb-20ms")]
    [Arguments("hybrid-mono-10ms")]
    [Arguments("hybrid-stereo-20ms")]
    [Arguments("opus-stereo-lowdelay")]
    [Arguments("opus-stereo-music")]
    [Arguments("celt-stereo-2p5ms")]
    [Arguments("celt-stereo-5ms")]
    [Arguments("celt-stereo-10ms")]
    [Arguments("celt-stereo-20ms-lowrate")]
    [Arguments("celt-mono-10ms-narrow")]
    public async Task File_AgreesWithTheReference(string name)
    {
        var decoded = Decode($"{name}.opus");
        var reference = Decode($"{name}.reference.flac");

        await Assert.That(Math.Abs(decoded.Count - reference.Count)).IsLessThanOrEqualTo(EndSlack);
        await Assert.That(RootMeanSquare(reference.Samples)).IsGreaterThan(MinimumLevel);
        await Assert.That(SignalToError(decoded.Samples, reference.Samples)).IsGreaterThanOrEqualTo(MinimumSignalToError);
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

    /// <summary>Gets the path of a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path.</returns>
    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>Opens a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static MediaContainer Open(string name) =>
        MediaContainers.Open(PathOf(name)) ?? throw new InvalidDataException($"{name} did not open.");

    /// <summary>Decodes a whole file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>All the sound.</returns>
    private static PcmBuffer Decode(string name)
    {
        using var container = Open(name);
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
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
