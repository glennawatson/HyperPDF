// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

extern alias PdfLibrary;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>Checks selection of reusable objects with different capacities.</summary>
public sealed class ReusableSlotsTests
{
    /// <summary>The two cache capacities retained at once.</summary>
    private const int SlotCount = 2;

    /// <summary>A matching entry remains reachable behind a non-matching one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchingTakeKeepsOtherEntriesAvailable()
    {
        var slots = new PdfLibrary::HyperPdfLibrary.Rendering.ReusableSlots<int[]>(SlotCount);
        var small = new int[1];
        var large = new int[SlotCount];
        await Assert.That(slots.Offer(small)).IsTrue();
        await Assert.That(slots.Offer(large)).IsTrue();

        var selected = slots.Take(SlotCount, static (item, count) => item.Length == count);

        await Assert.That(selected).IsSameReferenceAs(large);
        await Assert.That(slots.Take()).IsSameReferenceAs(small);
        await Assert.That(slots.Take()).IsNull();
    }

    /// <summary>An unmatched request leaves the retained entry available to its next user.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnmatchedTakeLeavesTheEntryInItsSlot()
    {
        var slots = new PdfLibrary::HyperPdfLibrary.Rendering.ReusableSlots<int[]>(1);
        var item = new int[1];
        await Assert.That(slots.Offer(item)).IsTrue();

        await Assert.That(slots.Take(SlotCount, static (entry, count) => entry.Length == count)).IsNull();
        await Assert.That(slots.Take()).IsSameReferenceAs(item);
    }
}
