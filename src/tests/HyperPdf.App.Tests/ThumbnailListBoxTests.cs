// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using HyperPdf.App.Controls;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks that thumbnail navigation leaves visible previews still.</summary>
public sealed class ThumbnailListBoxTests
{
    /// <summary>The test viewport height.</summary>
    private const double Viewport = 300;

    /// <summary>The content height.</summary>
    private const double Extent = 2000;

    /// <summary>The virtualized item count.</summary>
    private const int ItemCount = 80;

    /// <summary>The item height.</summary>
    private const double ItemHeight = 100;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 250;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 500;

    /// <summary>The second visible selection.</summary>
    private const int VisibleIndex = 1;

    /// <summary>The selection beyond the viewport.</summary>
    private const int OutsideIndex = 5;

    /// <summary>The visible selection after paging.</summary>
    private const int PreviousIndex = 4;

    /// <summary>A distant unrealized selection.</summary>
    private const int DistantIndex = 70;

    /// <summary>Half, for finding a middle.</summary>
    private const double Half = 0.5;

    /// <summary>Checks visible, clipped, distant and end-of-document thumbnails.</summary>
    /// <param name="offset">The starting offset.</param>
    /// <param name="top">The item top.</param>
    /// <param name="bottom">The item bottom.</param>
    /// <param name="expected">The expected offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(300, 320, 420, 300)]
    [Arguments(300, 580, 680, 480)]
    [Arguments(300, 240, 340, 140)]
    [Arguments(300, 600, 700, 500)]
    [Arguments(300, 200, 300, 100)]
    [Arguments(0, 1500, 1600, 1400)]
    [Arguments(1500, 0, 100, 0)]
    [Arguments(1500, 1900, 2000, 1700)]
    public async Task CentresItemsNotInFullView(double offset, double top, double bottom, double expected) =>
        await Assert.That(ThumbnailViewport.GetOffset(offset, Viewport, top, bottom, Extent)).IsEqualTo(expected);

    /// <summary>Checks that the list eases to a selection out of view and lands with it centred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EasesToSelection()
    {
        var list = new ThumbnailListBox { ItemsSource = Enumerable.Range(0, ItemCount).ToArray(), ItemTemplate = new FuncDataTemplate<int>(static (_, _) => new Border { Height = ItemHeight }) };
        var window = new Window { Content = list, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => list.Scroll is ScrollViewer { Viewport.Height: > 0 })).IsTrue();
            var scroll = (ScrollViewer)list.Scroll!;
            var offsets = new List<double>();
            using var recording = scroll.WhenChanged(static x => x.Offset).SubscribeSafe(offset => offsets.Add(offset.Y), static _ => { });
            list.SelectedIndex = OutsideIndex;
            var landed = await UiWait.UntilAsync(() => IsCentred(list, scroll, OutsideIndex));
            var target = scroll.Offset.Y;

            await Assert.That(landed).IsTrue();
            await Assert.That(offsets.Count(offset => offset > 0 && offset < target - 1)).IsGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Checks that a virtualized list centres selections out of view and keeps visible selections still.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsVisibleSelectionsStill()
    {
        var list = new ThumbnailListBox
        {
            ReduceMotion = true,
            ItemsSource = Enumerable.Range(0, ItemCount).ToArray(),
            ItemTemplate = new FuncDataTemplate<int>(static (_, _) => new Border { Height = ItemHeight }),
        };
        var window = new Window { Content = list, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => list.Scroll is ScrollViewer { Viewport.Height: > 0 })).IsTrue();
            var scroll = (ScrollViewer)list.Scroll!;
            var initial = scroll.Offset.Y;
            list.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(initial);
            list.SelectedIndex = VisibleIndex;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(initial);
            list.SelectedIndex = OutsideIndex;
            await Assert.That(await UiWait.UntilAsync(() => IsCentred(list, scroll, OutsideIndex))).IsTrue();
            var page = scroll.Offset.Y;
            list.SelectedIndex = PreviousIndex;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(page);
            list.SelectedIndex = 0;
            await Assert.That(await UiWait.UntilAsync(() => scroll.Offset.Y == 0)).IsTrue();
            list.SelectedIndex = DistantIndex;
            await Assert.That(await UiWait.UntilAsync(() => list.ContainerFromIndex(DistantIndex) is not null)).IsTrue();
            await Assert.That(scroll.Offset.Y).IsGreaterThan(page);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Determines whether an item sits in the middle of the viewport.</summary>
    /// <param name="list">The list.</param>
    /// <param name="scroll">The list's scroll viewer.</param>
    /// <param name="index">The item index.</param>
    /// <returns><see langword="true"/> when centred within a pixel.</returns>
    private static bool IsCentred(ThumbnailListBox list, ScrollViewer scroll, int index) =>
        list.ContainerFromIndex(index) is { } container && container.TranslatePoint(default, scroll) is { } position
        && Math.Abs(position.Y + (container.Bounds.Height * Half) - (scroll.Viewport.Height * Half)) < 1;
}
