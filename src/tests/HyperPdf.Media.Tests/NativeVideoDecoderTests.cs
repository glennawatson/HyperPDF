// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs;
using HyperPdf.Media.Native;

namespace HyperPdf.Media.Tests;

/// <summary>Checks that the decoders an operating system provides give the same pictures as the decoders written in C#.</summary>
public sealed class NativeVideoDecoderTests
{
    /// <summary>The H.264 fixture, which also holds sound and a caption track.</summary>
    private const string H264File = "av-h264-aac-text.mp4";

    /// <summary>The H.265 fixture.</summary>
    private const string H265File = "h265-b.mp4";

    /// <summary>The number of samples that may differ between the two decoders: none, because H.264 and H.265 decoding is exact.</summary>
    private const int AllowedDifferences = 0;

    /// <summary>The fixtures: H.264 files that exercise intra, predicted and bi-predicted pictures, weights and loop filters, then H.265 files that do the same.</summary>
    private static readonly string[] FixtureNames =
    [
        H264File,
        "h264-intra-baseline.mp4",
        "h264-p-cabac.mp4",
        "h264-p-cabac-weighted.mp4",
        "h264-p-cavlc-multiref.mp4",
        "h264-p-cavlc-partitions.mp4",
        "h264-b-cabac-deblock.mp4",
        "h264-b-cavlc-pyramid.mp4",
        "h264-b-cavlc-implicit.mp4",
        "h264-b-cavlc-temporal.mp4",
        "h264-b-cabac-spatial.mp4",
        "h264-high-intra-cabac.mp4",
        "h264-high-p-cabac.mp4",
        "h264-deblock-offsets.mp4",
        H265File,
        "h265-intra.mp4",
        "h265-p.mp4",
        "h265-p-multiref.mp4",
        "h265-p-asymmetric.mp4",
        "h265-p-no-temporal-mvp.mp4",
        "h265-b-pyramid.mp4",
        "h265-b-weighted.mp4",
        "h265-b-long-term.mp4",
        "h265-cropped.mp4",
        "h265-ctu16.mp4",
        "h265-wavefront.mp4",
        "h265-intra-wavefront.mp4",
        "h265-dqp.mp4",
        "h265-transform-skip.mp4",
        "h265-lossless.mp4",
        "h265-sao-only.mp4",
        "h265-deblock-only.mp4",
        "h265-loop-filters-ctu32.mp4",
        "h265-loop-filters-ctu16-b.mp4",
        "h265-deblock-offsets-ctu16.mp4",
        "h265-deblock-negative-offsets-ctu16.mp4",
        "h265-intra-ctu32.mp4",
        "h265-intra-constrained.mp4",
    ];

    /// <summary>Gets the fixtures as test cases.</summary>
    /// <returns>The cases.</returns>
    public static IEnumerable<Func<string>> Files() => FixtureNames.Select(static name => (Func<string>)(() => name));

    /// <summary>The decoder of this system gives the pictures the C# decoder gives, in the same order and with the same samples.</summary>
    /// <param name="file">The fixture.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The fixture did not open.</exception>
    /// <exception cref="NotSupportedException">The C# decoders do not read the fixture.</exception>
    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task SystemDecoderMatchesTheManagedDecoder(string file)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            Skip.Test("This system has no decoder for the test to compare.");
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        using var container = MediaContainers.Open(path) ?? throw new InvalidDataException($"{file} did not open.");
        var track = container.Tracks[container.FindTrack(MediaTrackKind.Video)];
        using var native = PlatformVideoDecoders.TryCreateNative(track, out var reason);
        if (native is null)
        {
            Skip.Test($"This system has no decoder for the coding: {reason}");
        }

        using var managed = VideoDecoders.Create(track) ?? throw new NotSupportedException($"No C# decoder for {file}.");
        var expected = Decode(path, managed);
        var actual = Decode(path, native!);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        var differences = 0;
        for (var i = 0; i < expected.Count; i++)
        {
            differences += Differences(expected[i], actual[i]);
        }

        await Assert.That(differences).IsEqualTo(AllowedDifferences);
    }

    /// <summary>Decodes every picture of a file's first video track.</summary>
    /// <param name="path">The file.</param>
    /// <param name="decoder">The decoder for the track.</param>
    /// <returns>The pictures, in output order.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static List<VideoFrame> Decode(string path, VideoDecoder decoder)
    {
        using var container = MediaContainers.Open(path) ?? throw new InvalidDataException($"{path} did not open.");
        var index = container.FindTrack(MediaTrackKind.Video);
        var frames = new List<VideoFrame>();
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            if (packet.Track == index)
            {
                decoder.Decode(packet, frames);
            }
        }

        decoder.Flush(frames);
        decoder.Reset();
        return frames;
    }

    /// <summary>Counts the samples that differ between two pictures.</summary>
    /// <param name="expected">The first picture.</param>
    /// <param name="actual">The second picture.</param>
    /// <returns>The number of samples that differ.</returns>
    private static int Differences(VideoFrame expected, VideoFrame actual)
    {
        var count = 0;
        count += expected.Y.AsSpan().CommonPrefixLength(actual.Y) == expected.Y.Length ? 0 : CountDifferent(expected.Y, actual.Y);
        count += expected.U.AsSpan().CommonPrefixLength(actual.U) == expected.U.Length ? 0 : CountDifferent(expected.U, actual.U);
        count += expected.V.AsSpan().CommonPrefixLength(actual.V) == expected.V.Length ? 0 : CountDifferent(expected.V, actual.V);
        return count;
    }

    /// <summary>Counts the positions where two runs of samples differ.</summary>
    /// <param name="first">The first run.</param>
    /// <param name="second">The second run.</param>
    /// <returns>The number of positions that differ.</returns>
    private static int CountDifferent(byte[] first, byte[] second)
    {
        var count = Math.Abs(first.Length - second.Length);
        for (var i = 0; i < Math.Min(first.Length, second.Length); i++)
        {
            count += first[i] == second[i] ? 0 : 1;
        }

        return count;
    }
}
