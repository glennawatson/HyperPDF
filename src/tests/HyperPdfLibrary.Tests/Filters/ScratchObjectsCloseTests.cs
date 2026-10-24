// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks that scratch objects offered while their owner closes are never left behind.</summary>
[NotInParallel]
public sealed class ScratchObjectsCloseTests
{
    /// <summary>The owners closed while an object is offered.</summary>
    private const int Rounds = 3000;

    /// <summary>How long a round may wait for its two threads.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>An offer that is accepted while the owner closes ends with the object disposed, never idle in a slot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnOfferRacingCloseNeverStrandsTheObject()
    {
        var stranded = 0;
        var leaked = 0;
        for (var round = 0; round < Rounds; round++)
        {
            var owner = new ScratchPools();
            var objects = owner.Objects<Item>();
            var item = new Item();
            using var start = new ManualResetEventSlim(false);
            var offered = false;
            var offer = new Thread(() =>
            {
                _ = start.Wait(Patience);
                offered = objects.Offer(item);
            });
            var close = new Thread(() =>
            {
                _ = start.Wait(Patience);
                owner.Close();
            });
            offer.Start();
            close.Start();
            start.Set();
            _ = offer.Join(Patience);
            _ = close.Join(Patience);

            if (objects.Take() is not null)
            {
                stranded++;
            }

            if (offered && !item.IsDisposed)
            {
                leaked++;
            }
        }

        await Assert.That(stranded).IsEqualTo(0);
        await Assert.That(leaked).IsEqualTo(0);
    }

    /// <summary>An object kept for reuse.</summary>
    private sealed class Item : IDisposable
    {
        /// <summary>Gets a value indicating whether the object was disposed.</summary>
        internal bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }
}
