// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks cache order across inline storage, overflow growth and eviction.</summary>
public sealed class PagePictureEntriesTests
{
    /// <summary>The entries in the removal scenarios.</summary>
    private const int EntryCount = 4;

    /// <summary>Removing an entry keeps the order of the remaining pages.</summary>
    /// <param name="removed">The removed entry's position.</param>
    /// <param name="first">The oldest remaining page.</param>
    /// <param name="second">The middle remaining page.</param>
    /// <param name="third">The newest remaining page.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 1, 2, 3)]
    [Arguments(1, 0, 2, 3)]
    [Arguments(2, 0, 1, 3)]
    [Arguments(3, 0, 1, 2)]
    public async Task RemovingAnEntryPreservesLruOrder(int removed, int first, int second, int third)
    {
        PagePictureEntries entries = default;
        for (var i = 0; i < EntryCount; i++)
        {
            entries.Add(new(i, 0));
        }

        entries.RemoveAt(removed);

        await Assert.That(entries.Count).IsEqualTo(EntryCount - 1);
        await Assert.That(entries[0].PageIndex).IsEqualTo(first);
        await Assert.That(entries[1].PageIndex).IsEqualTo(second);
        await Assert.That(entries[2].PageIndex).IsEqualTo(third);
    }

    /// <summary>Bulk eviction and clearing keep a grown cache usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BulkEvictionAndClearPreserveLaterEntries()
    {
        const int grownCount = 8;
        const int kept = 2;
        PagePictureEntries entries = default;
        for (var i = 0; i < grownCount; i++)
        {
            entries.Add(new(i, 0));
        }

        entries.RemoveFirst(grownCount - kept);
        await Assert.That(entries.Count).IsEqualTo(kept);
        await Assert.That(entries[0].PageIndex).IsEqualTo(grownCount - kept);
        await Assert.That(entries[1].PageIndex).IsEqualTo(grownCount - 1);

        entries.Clear();
        entries.Add(new(EntryCount, 0));
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].PageIndex).IsEqualTo(EntryCount);
        entries.RemoveFirst(1);
        await Assert.That(entries.Count).IsEqualTo(0);
    }
}
