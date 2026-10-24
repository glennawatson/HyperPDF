// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the sidebar scrolls only when a thumbnail is not fully in view.</summary>
public sealed class ThumbnailViewportTests
{
    /// <summary>The height of the sidebar's viewport.</summary>
    private const double Viewport = 400;

    /// <summary>The height of all the thumbnails together.</summary>
    private const double Extent = 2000;

    /// <summary>The height of one thumbnail.</summary>
    private const double Height = 100;

    /// <summary>The scroll offset the tests start from.</summary>
    private const double Offset = 100;

    /// <summary>The farthest the sidebar can scroll.</summary>
    private const double MaxOffset = Extent - Viewport;

    /// <summary>The offset that centres the thumbnail whose top is at 800: its middle (850) less half the viewport (200).</summary>
    private const double CentredOffset = 650;

    /// <summary>A thumbnail that is fully visible leaves the offset alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleThumbnailKeepsTheOffset()
    {
        const double top = Offset + Height;

        var offset = ThumbnailViewport.GetOffset(Offset, Viewport, top, top + Height, Extent);

        await Assert.That(offset).IsEqualTo(Offset);
    }

    /// <summary>A thumbnail below the view is centred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThumbnailBelowTheViewIsCentred()
    {
        const double top = 800;
        var offset = ThumbnailViewport.GetOffset(0, Viewport, top, top + Height, Extent);

        await Assert.That(offset).IsEqualTo(CentredOffset);
    }

    /// <summary>Centring never scrolls before the first thumbnail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentringStopsAtTheStart()
    {
        var offset = ThumbnailViewport.GetOffset(MaxOffset, Viewport, 0, Height, Extent);

        await Assert.That(offset).IsEqualTo(0);
    }

    /// <summary>Centring never scrolls past the last thumbnail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentringStopsAtTheEnd()
    {
        var offset = ThumbnailViewport.GetOffset(0, Viewport, Extent - Height, Extent, Extent);

        await Assert.That(offset).IsEqualTo(MaxOffset);
    }

    /// <summary>A view with no height cannot show anything, so the offset is unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyViewKeepsTheOffset()
    {
        var offset = ThumbnailViewport.GetOffset(Offset, 0, Extent - Height, Extent, Extent);

        await Assert.That(offset).IsEqualTo(Offset);
    }
}
