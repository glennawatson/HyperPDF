// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests for <see cref="FfmpegArguments"/> and <see cref="MediaToolLocator"/>.</summary>
public sealed class FfmpegArgumentsTests
{
    /// <summary>Where the video test starts, in seconds.</summary>
    private const double StartSeconds = 1.5;

    /// <summary>The pictures each second the video test asks for.</summary>
    private const int Rate = 15;

    /// <summary>The bytes in each pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A start before the beginning, in seconds.</summary>
    private const int BeforeTheStart = -3;

    /// <summary>The size and rate the video test asks for.</summary>
    private static readonly FrameSize VideoSize = new(320, 240);

    /// <summary>A high definition video size.</summary>
    private static readonly FrameSize HighDefinition = new(1920, 1080);

    /// <summary>What a high definition video is scaled to.</summary>
    private static readonly FrameSize Scaled = new(640, 360);

    /// <summary>An odd sized video.</summary>
    private static readonly FrameSize Odd = new(161, 121);

    /// <summary>What an odd sized video is rounded to.</summary>
    private static readonly FrameSize Even = new(160, 120);

    /// <summary>A video of a single pixel.</summary>
    private static readonly FrameSize SinglePixel = new(1, 1);

    /// <summary>What a single pixel is raised to.</summary>
    private static readonly FrameSize Smallest = new(2, 2);

    /// <summary>A picture whose size is known in bytes.</summary>
    private static readonly FrameSize Tiny = new(2, 3);

    /// <summary>Verifies the video arguments seek and pace on the input, keep the path whole and ask for raw pictures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VideoArgumentsPaceTheInput()
    {
        const string path = "/tmp/a clip with spaces.mp4";
        var arguments = FfmpegArguments.Video(path, TimeSpan.FromSeconds(StartSeconds), VideoSize, Rate);

        await Assert.That(arguments).Contains(path);
        await Assert.That(arguments).Contains("1.5");
        await Assert.That(Array.IndexOf(arguments, "-re")).IsLessThan(Array.IndexOf(arguments, "-i"));
        await Assert.That(Array.IndexOf(arguments, "-ss")).IsLessThan(Array.IndexOf(arguments, "-i"));
        await Assert.That(arguments).Contains("fps=15,scale=320:240");
        await Assert.That(arguments).Contains("rawvideo");
    }

    /// <summary>Verifies the sound arguments play without a window from the start point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundArgumentsHaveNoWindow()
    {
        var arguments = FfmpegArguments.Audio("/tmp/a.wav", TimeSpan.FromSeconds(BeforeTheStart));

        await Assert.That(arguments).Contains("-nodisp");
        await Assert.That(arguments).Contains("-autoexit");
        await Assert.That(arguments[Array.IndexOf(arguments, "-ss") + 1]).IsEqualTo("0");
    }

    /// <summary>Verifies large pictures shrink to the limit, sides are even, and an unknown size stays empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesFitTheLimitWithEvenSides()
    {
        await Assert.That(FfmpegArguments.Fit(HighDefinition.Width, HighDefinition.Height)).IsEqualTo(Scaled);
        await Assert.That(FfmpegArguments.Fit(Odd.Width, Odd.Height)).IsEqualTo(Even);
        await Assert.That(FfmpegArguments.Fit(SinglePixel.Width, SinglePixel.Height)).IsEqualTo(Smallest);
        await Assert.That(FfmpegArguments.Fit(0, 1).IsEmpty).IsTrue();
        await Assert.That(Tiny.ByteCount).IsEqualTo(Tiny.Width * Tiny.Height * BytesPerPixel);
    }

    /// <summary>Verifies the tools are found in the preferred folder and the search path, and a missing tool gives nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToolsAreFoundOnTheSearchPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tools-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            await Assert.That(MediaToolLocator.Find(directory, null)).IsNull();

            foreach (var name in new[] { "ffmpeg", "ffplay", "ffprobe" })
            {
                await File.WriteAllTextAsync(Path.Combine(directory, name + suffix), string.Empty);
            }

            var onPath = MediaToolLocator.Find(directory, null);
            var preferred = MediaToolLocator.Find(null, directory);

            await Assert.That(onPath).IsNotNull();
            await Assert.That(Path.GetFileName(onPath!.Ffplay)).IsEqualTo($"ffplay{suffix}");
            await Assert.That(preferred).IsEqualTo(onPath);
            await Assert.That(MediaToolLocator.Find(null, null)).IsNull();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
