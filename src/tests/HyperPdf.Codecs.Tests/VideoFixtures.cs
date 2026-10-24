// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdf.Codecs.Tests;

/// <summary>Decodes video fixtures and checks their pictures against lists of frame hashes.</summary>
internal static class VideoFixtures
{
    /// <summary>The folder next to the test assembly that holds the fixtures.</summary>
    private const string FixtureFolder = "Fixtures";

    /// <summary>Decodes a fixture and checks every picture against the hash list of the same name.</summary>
    /// <param name="name">The name of the fixture and of its hash list, without the extension.</param>
    /// <returns>A task representing the asynchronous check.</returns>
    internal static async Task AssertPicturesMatchAsync(string name)
    {
        var frames = Decode($"{name}.mp4");
        var expected = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, FixtureFolder, $"{name}.sha256"));

        await Assert.That(frames.Count).IsEqualTo(expected.Length);
        for (var n = 0; n < frames.Count; n++)
        {
            await Assert.That(HashOf(frames[n])).IsEqualTo(expected[n]);
        }
    }

    /// <summary>Decodes a fixture handing every picture back to the decoder as soon as it is hashed, and checks the hashes against the hash list of the same name.</summary>
    /// <param name="name">The name of the fixture and of its hash list, without the extension.</param>
    /// <returns>A task representing the asynchronous check.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    /// <exception cref="NotSupportedException">No decoder handles the track.</exception>
    internal static async Task AssertRecycledPicturesMatchAsync(string name)
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, FixtureFolder, $"{name}.mp4")) ?? throw new InvalidDataException($"{name} did not open.");
        var index = container.FindTrack(MediaTrackKind.Video);
        using var decoder = VideoDecoders.Create(container.Tracks[index]) ?? throw new NotSupportedException($"No decoder for {name}.");
        var hashes = new List<string>();
        var frames = new List<VideoFrame>();
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            if (packet.Track != index)
            {
                continue;
            }

            decoder.Decode(packet, frames);
            HashAndRecycle(decoder, frames, hashes);
        }

        decoder.Flush(frames);
        HashAndRecycle(decoder, frames, hashes);
        var expected = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, FixtureFolder, $"{name}.sha256"));

        await Assert.That(hashes.Count).IsEqualTo(expected.Length);
        for (var n = 0; n < hashes.Count; n++)
        {
            await Assert.That(hashes[n]).IsEqualTo(expected[n]);
        }
    }

    /// <summary>Hashes every picture, hands it back to the decoder, and empties the list.</summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="frames">The pictures.</param>
    /// <param name="hashes">Receives the hashes.</param>
    private static void HashAndRecycle(VideoDecoder decoder, List<VideoFrame> frames, List<string> hashes)
    {
        foreach (var frame in frames)
        {
            hashes.Add(HashOf(frame));
            decoder.Recycle(frame);
        }

        frames.Clear();
    }

    /// <summary>Hashes the three planes of a frame in the order a raw yuv file stores them.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    private static string HashOf(VideoFrame frame)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(frame.Y);
        hash.AppendData(frame.U);
        hash.AppendData(frame.V);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Decodes every picture of a file's first video track.</summary>
    /// <param name="name">The fixture name.</param>
    /// <returns>The pictures, in output order.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    /// <exception cref="NotSupportedException">No decoder handles the track.</exception>
    private static List<VideoFrame> Decode(string name)
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, FixtureFolder, name)) ?? throw new InvalidDataException($"{name} did not open.");
        var index = container.FindTrack(MediaTrackKind.Video);
        using var decoder = VideoDecoders.Create(container.Tracks[index]) ?? throw new NotSupportedException($"No decoder for {name}.");
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
        return frames;
    }
}
