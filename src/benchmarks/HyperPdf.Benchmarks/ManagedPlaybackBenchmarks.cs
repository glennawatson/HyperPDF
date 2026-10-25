// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Media;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>Measures opening a clip and playing it to its end with the built-in decoders; allocations come from the EventPipe trace.</summary>
public sealed class ManagedPlaybackBenchmarks : IDisposable
{
    /// <summary>The longest to wait for the clip to end, in seconds.</summary>
    private const int WaitSeconds = 30;

    /// <summary>The clip: two seconds of H.264 pictures with AAC sound and text.</summary>
    private readonly string _clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "av-h264-aac-text.mp4");

    /// <summary>The player.</summary>
    private ManagedMediaPlayer _player = null!;

    /// <summary>Makes the player.</summary>
    [GlobalSetup]
    public void Setup() => _player = new(static () => NullStreamingAudioOutput.Instance, false);

    /// <summary>Stops the player.</summary>
    [GlobalCleanup]
    public void Dispose() => _player?.Dispose();

    /// <summary>Opens the clip and reads what it holds.</summary>
    /// <returns>A task that completes with the width of the picture.</returns>
    [Benchmark]
    public async Task<int> OpenClip() =>
        await _player.OpenAsync(_clip, CancellationToken.None).ConfigureAwait(false) ? _player.FrameSize.Width : 0;

    /// <summary>Plays the clip to its end, counting the pictures shown.</summary>
    /// <returns>A task that completes with the number of pictures shown.</returns>
    [Benchmark]
    public async Task<int> PlayClip()
    {
        if (!await _player.OpenAsync(_clip, CancellationToken.None).ConfigureAwait(false))
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
