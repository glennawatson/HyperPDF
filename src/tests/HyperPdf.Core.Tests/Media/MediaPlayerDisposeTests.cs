// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests that disposing a media player while a picture thread publishes never throws.</summary>
public sealed class MediaPlayerDisposeTests
{
    /// <summary>The players disposed while a thread publishes.</summary>
    private const int Rounds = 200;

    /// <summary>Publishing during disposal completes without an exception.</summary>
    /// <returns>A task that completes when the check ends.</returns>
    [Test]
    public async Task PublishingWhileDisposingDoesNotThrow()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var player = new PublishingPlayer();
            using var started = new ManualResetEventSlim(false);
            Exception? failure = null;
            var publisher = new Thread(() =>
            {
                started.Set();
                try
                {
                    for (var i = 0; i < Rounds; i++)
                    {
                        player.PublishNow();
                    }
                }
                catch (ObjectDisposedException exception)
                {
                    failure = exception;
                }
            });
            publisher.Start();
            started.Wait();
            player.Dispose();
            publisher.Join();

            await Assert.That(failure).IsNull();
        }
    }

    /// <summary>A player whose only job is to publish on demand.</summary>
    private sealed class PublishingPlayer : MediaPlayerBase
    {
        /// <summary>Publishes the current snapshot.</summary>
        public void PublishNow() => Publish();

        /// <inheritdoc />
        public override Task<bool> OpenAsync(string path, CancellationToken cancellationToken) => Task.FromResult(false);

        /// <inheritdoc />
        protected override Task<string?> PlayAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
