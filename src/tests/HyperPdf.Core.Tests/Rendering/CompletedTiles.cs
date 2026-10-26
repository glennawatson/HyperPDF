// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Takes the tiles a <see cref="RenderScheduler"/> has finished, waiting on its completion signal rather than a clock.</summary>
internal static class CompletedTiles
{
    /// <summary>Takes tiles, in the order the scheduler finished them, until the wanted number has arrived.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="completed">Released once for every completion signal the scheduler raises.</param>
    /// <param name="count">The number of tiles to take.</param>
    /// <param name="cancellationToken">Ends the wait when the test is cancelled.</param>
    /// <returns>The keys taken; their surfaces are disposed.</returns>
    internal static async Task<List<TileKey>> CollectAsync(RenderScheduler scheduler, SemaphoreSlim completed, int count, CancellationToken cancellationToken)
    {
        var order = new List<TileKey>();
        while (order.Count < count)
        {
            await completed.WaitAsync(cancellationToken);
            Drain(scheduler, order);
        }

        return order;
    }

    /// <summary>Takes every tile that is ready now.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="order">Receives the keys taken.</param>
    internal static void Drain(RenderScheduler scheduler, List<TileKey> order)
    {
        while (scheduler.TryTakeCompleted(out var tile))
        {
            order.Add(tile.Key);
            tile.Surface.Dispose();
        }
    }
}
