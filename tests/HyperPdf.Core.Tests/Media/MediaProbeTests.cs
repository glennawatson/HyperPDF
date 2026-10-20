// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests for <see cref="MediaProbe"/>.</summary>
public sealed class MediaProbeTests
{
    /// <summary>The text ffprobe prints for a file with a picture, sound and captions.</summary>
    private const string FullFile = "codec_type=video\nwidth=160\nheight=120\ncodec_type=audio\ncodec_type=subtitle\nwidth=N/A\nheight=N/A\nduration=2.000000\n";

    /// <summary>The width in <see cref="FullFile"/>.</summary>
    private const int FullWidth = 160;

    /// <summary>The height in <see cref="FullFile"/>.</summary>
    private const int FullHeight = 120;

    /// <summary>The length in seconds in <see cref="FullFile"/>.</summary>
    private const double FullSeconds = 2;

    /// <summary>The width of the video stream in the file that also has a caption stream with a size.</summary>
    private const int VideoWidth = 32;

    /// <summary>The height of the video stream in the file that also has a caption stream with a size.</summary>
    private const int VideoHeight = 16;

    /// <summary>The length in seconds of the sound-only file.</summary>
    private const double SoundSeconds = 61.5;

    /// <summary>Verifies the streams, size and length are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsStreamsSizeAndLength()
    {
        var info = MediaProbe.Parse(FullFile);

        await Assert.That(info.HasVideo).IsTrue();
        await Assert.That(info.HasAudio).IsTrue();
        await Assert.That(info.HasCaptions).IsTrue();
        await Assert.That(info.Width).IsEqualTo(FullWidth);
        await Assert.That(info.Height).IsEqualTo(FullHeight);
        await Assert.That(info.Duration).IsEqualTo(TimeSpan.FromSeconds(FullSeconds));
    }

    /// <summary>Verifies the size of a caption stream does not replace the size of the picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyTheVideoStreamGivesTheSize()
    {
        var info = MediaProbe.Parse("codec_type=subtitle\nwidth=640\nheight=480\ncodec_type=video\nwidth=32\nheight=16\n");

        await Assert.That(info.Width).IsEqualTo(VideoWidth);
        await Assert.That(info.Height).IsEqualTo(VideoHeight);
    }

    /// <summary>Verifies a file with sound only has no picture size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundOnlyFileHasNoPicture()
    {
        var info = MediaProbe.Parse("codec_type=audio\nduration=61.5\n");

        await Assert.That(info.HasVideo).IsFalse();
        await Assert.That(info.HasAudio).IsTrue();
        await Assert.That(info.Width).IsEqualTo(0);
        await Assert.That(info.Duration).IsEqualTo(TimeSpan.FromSeconds(SoundSeconds));
    }

    /// <summary>Verifies empty, damaged and out-of-range text gives an empty result and never throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedTextGivesNothing()
    {
        await Assert.That(MediaProbe.Parse(string.Empty).HasVideo).IsFalse();
        await Assert.That(MediaProbe.Parse("=\n==\nnonsense\n").HasAudio).IsFalse();
        await Assert.That(MediaProbe.Parse("duration=-5\n").Duration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(MediaProbe.Parse("duration=1e300\n").Duration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(MediaProbe.Parse("duration=N/A\n").Duration).IsEqualTo(TimeSpan.Zero);
    }
}
