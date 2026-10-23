// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Media;
using HyperPdf.Media;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures opening a clip and playing it to its end with the FFmpeg libraries. They do nothing when the libraries or the FFmpeg
/// program that makes the clip are missing; allocations come from the EventPipe trace.
/// </summary>
public sealed class LibavPlaybackBenchmarks : IDisposable
{
    /// <summary>The longest to wait for the clip to end, in seconds.</summary>
    private const int WaitSeconds = 30;

    /// <summary>The arguments that make a two second clip of 160 by 120 pixels with a picture and silent sound, before the file name.</summary>
    private static readonly string[] ClipArguments =
    [
        "-v", "error", "-f", "lavfi", "-i", "testsrc=duration=2:size=160x120:rate=10", "-f", "lavfi", "-i", "sine=frequency=440:duration=2,volume=0", "-c:v", "mpeg4", "-c:a", "aac", "-y",
    ];

    /// <summary>The folder that holds the clip.</summary>
    private string _folder = string.Empty;

    /// <summary>The clip, or empty when it could not be made.</summary>
    private string _clip = string.Empty;

    /// <summary>The player.</summary>
    private LibavMediaPlayer _player = null!;

    /// <summary>Makes the clip.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _player = new(static () => NullStreamingAudioOutput.Instance, false);
        var tools = MediaToolLocator.Find();
        if (!FfmpegLibraries.IsAvailable || tools is null)
        {
            return;
        }

        _folder = Path.Combine(Path.GetTempPath(), $"libav-bench-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "clip.mp4");
        var start = new ProcessStartInfo(tools.Ffmpeg) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in ClipArguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.ArgumentList.Add(path);
        using var process = Process.Start(start)!;
        _ = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        _clip = File.Exists(path) ? path : string.Empty;
    }

    /// <summary>Stops the player and deletes the clip.</summary>
    [GlobalCleanup]
    public void Dispose()
    {
        _player?.Dispose();
        if (_folder.Length != 0 && Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    /// <summary>Opens the clip and reads what it holds.</summary>
    /// <returns>A task that completes with the width of the picture.</returns>
    [Benchmark]
    public async Task<int> OpenClip() =>
        _clip.Length != 0 && await _player.OpenAsync(_clip, CancellationToken.None).ConfigureAwait(false) ? _player.Info.Width : 0;

    /// <summary>Plays the clip to its end, counting the pictures shown.</summary>
    /// <returns>A task that completes with the number of pictures shown.</returns>
    [Benchmark]
    public async Task<int> PlayClip()
    {
        if (_clip.Length == 0 || !await _player.OpenAsync(_clip, CancellationToken.None).ConfigureAwait(false))
        {
            return 0;
        }

        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = _player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State is MediaPlayerState.Ended or MediaPlayerState.Failed)
                {
                    _ = ended.TrySetResult();
                }
            },
            static _ => { });
        _player.Play();
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds)).ConfigureAwait(false);
        return _player.Snapshot.FrameVersion;
    }
}
