// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Rendering;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the list of pages scrolls only when the page being read reaches an edge of it.</summary>
public sealed class SidebarFollowTests
{
    /// <summary>The rows that fit the panel in these tests.</summary>
    private const int Visible = 5;

    /// <summary>The rows in the list in these tests.</summary>
    private const int Count = 50;

    /// <summary>The height of a row, in the units the Avalonia list measured in.</summary>
    private const int RowHeight = 100;

    /// <summary>The distance between the first rows tried.</summary>
    private const int Step = 3;

    /// <summary>The first row shown in these tests.</summary>
    private const int First = 10;

    /// <summary>The row just past the bottom edge: the first row, then the rows that fit.</summary>
    private const int PastBottom = First + Visible;

    /// <summary>The row just above the top edge.</summary>
    private const int AboveTop = First - 1;

    /// <summary>A page that is in view, or is not in the list, leaves the list where it is.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(10)]
    [Arguments(12)]
    [Arguments(14)]
    [Arguments(-1)]
    [Arguments(Count)]
    public async Task PageInViewOrUnknownDoesNotScroll(int page) => await Assert.That(SidebarFollow.First(First, Visible, Count, page)).IsEqualTo(First);

    /// <summary>The page just past the bottom edge scrolls the list down by one row, not more.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageJustPastTheBottomScrollsOneRow() => await Assert.That(SidebarFollow.First(First, Visible, Count, PastBottom)).IsEqualTo(First + 1);

    /// <summary>The page just above the top edge scrolls the list up by one row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageJustAboveTheTopScrollsOneRow() => await Assert.That(SidebarFollow.First(First, Visible, Count, AboveTop)).IsEqualTo(AboveTop);

    /// <summary>
    /// The list moves for exactly the pages the Avalonia version moved for: it was still while the page was fully in view and moved
    /// otherwise. Only where it moves to differs, and that is the one-row step asked for.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesForTheSamePagesAsTheAvaloniaList()
    {
        var disagreements = 0;
        for (var first = 0; first <= Count - Visible; first += Step)
        {
            for (var page = 0; page < Count; page++)
            {
                var offset = ThumbnailViewport.GetOffset(first * RowHeight, Visible * RowHeight, page * RowHeight, (page + 1) * RowHeight, Count * RowHeight);
                var avaloniaStays = Math.Abs(offset - (first * RowHeight)) < 1;
                var staysStill = SidebarFollow.First(first, Visible, Count, page) == first;
                disagreements += avaloniaStays == staysStill ? 0 : 1;
            }
        }

        await Assert.That(disagreements).IsEqualTo(0);
    }

    /// <summary>A jump far down or far up puts the page at the top of the list.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(30)]
    [Arguments(2)]
    public async Task FarJumpPutsThePageAtTheTop(int page) => await Assert.That(SidebarFollow.First(First, Visible, Count, page)).IsEqualTo(page);
}
