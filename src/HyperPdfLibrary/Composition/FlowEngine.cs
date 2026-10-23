// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Places a sequence of flowable items at a cursor, starting pages as they fill.</summary>
internal static class FlowEngine
{
    /// <summary>The most pages one item may be spread over, a guard against an item that never places anything.</summary>
    private const int MaximumPagesPerItem = 10_000;

    /// <summary>Places items one after another.</summary>
    /// <param name="items">The items, in order.</param>
    /// <param name="cursor">The cursor, which moves down as items are placed.</param>
    /// <param name="cancellationToken">Cancels the layout between items.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Run(List<Flowable> items, FlowCursor cursor, CancellationToken cancellationToken)
    {
        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];
            if (item.PageBreakBefore && !cursor.AtTop && cursor.CanPage)
            {
                cursor.NewPage();
            }

            if (item.KeepWithNext && cursor.CanPage)
            {
                KeepWithFollowing(items, i, cursor);
            }

            cursor.ApplySpace(item.SpaceBefore);
            PlaceWhole(item, cursor);
            cursor.PendingSpace = item.SpaceAfter;
        }
    }

    /// <summary>Starts a new page when an item that stays with what follows would be left at the foot of a page without its follower.</summary>
    /// <param name="items">The items.</param>
    /// <param name="index">The place of the item.</param>
    /// <param name="cursor">The cursor.</param>
    private static void KeepWithFollowing(List<Flowable> items, int index, FlowCursor cursor)
    {
        var height = 0F;
        var end = index;
        while (true)
        {
            height += items[end].MeasureStart(cursor) + (end > index ? items[end].SpaceBefore : 0F);
            if (!items[end].KeepWithNext || end + 1 >= items.Count)
            {
                break;
            }

            end++;
        }

        if (!cursor.AtTop && height > cursor.Remaining && height <= cursor.Top - cursor.BottomBase)
        {
            cursor.NewPage();
        }
    }

    /// <summary>Places an item, starting new pages until all of it is placed.</summary>
    /// <param name="item">The item.</param>
    /// <param name="cursor">The cursor.</param>
    /// <exception cref="InvalidOperationException">The item cannot be placed.</exception>
    private static void PlaceWhole(Flowable item, FlowCursor cursor)
    {
        for (var pages = 0; item.Place(cursor) == PlaceResult.NeedsPage; pages++)
        {
            if (pages >= MaximumPagesPerItem)
            {
                throw new InvalidOperationException("An item could not be placed on any page.");
            }

            cursor.NewPage();
        }
    }
}
