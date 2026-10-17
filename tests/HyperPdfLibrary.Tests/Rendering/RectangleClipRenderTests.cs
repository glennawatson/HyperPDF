// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that clipping to a rectangle gives the same pixels as clipping to an equal path that is not recognised as one.</summary>
[NotInParallel]
public sealed class RectangleClipRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The paint that covers the page, so the clip decides every pixel.</summary>
    private const string Paint = "0 0 1 rg 0 0 100 100 re f";

    /// <summary>Clips with fractional edges give the same pixels from a rectangle operator and from an equal path of lines.</summary>
    /// <param name="turns">The quarter turns the page is rendered at.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task RectangleClipMatchesEqualPathClip(int turns)
    {
        var rectangle = Render($"10.3 12.7 40.45 33.6 re W n {Paint}", turns);

        // A sixth command, a line back to the start, keeps the path from being read as a rectangle.
        var path = Render($"10.3 12.7 m 50.75 12.7 l 50.75 46.3 l 10.3 46.3 l 10.3 12.7 l h W n {Paint}", turns);

        await Assert.That(path.Pixels).IsEquivalentTo(rectangle.Pixels);
    }

    /// <summary>Nested clips with fractional edges and a rotated page match too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedRectangleClipsMatchEqualPathClips()
    {
        var rectangles = Render($"q 5.5 5.25 80.1 70.9 re W n q 20.2 15.8 60.6 50.4 re W n {Paint} Q Q", 1);
        var paths = Render(
            $"q 5.5 5.25 m 85.6 5.25 l 85.6 76.15 l 5.5 76.15 l 5.5 5.25 l h W n q 20.2 15.8 m 80.8 15.8 l 80.8 66.2 l 20.2 66.2 l 20.2 15.8 l h W n {Paint} Q Q",
            1);

        await Assert.That(paths.Pixels).IsEquivalentTo(rectangles.Pixels);
    }

    /// <summary>An empty clipping path leaves no paintable pixels.</summary>
    /// <param name="clip">The winding or even-odd clipping operators.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("W n")]
    [Arguments("W* n")]
    public async Task EmptyClipMatchesBlankPage(string clip)
    {
        var clipped = Render($"{clip} {Paint}", 0);
        var blank = Render(string.Empty, 0);
        await Assert.That(clipped.Pixels).IsEquivalentTo(blank.Pixels);
    }

    /// <summary>Restoring the graphics state removes an empty clip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestoreRemovesEmptyClip()
    {
        var restored = Render($"q W n {Paint} Q {Paint}", 0);
        var painted = Render(Paint, 0);
        await Assert.That(restored.Pixels).IsEquivalentTo(painted.Pixels);
    }

    /// <summary>Renders a page of the content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="turns">The quarter turns.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, int turns)
    {
        using var page = new RenderTestPage(new RenderTestPdf(Size, Size) { Content = content }.ToBytes());
        return page.RenderPage(1, turns, PdfRenderFlags.None);
    }
}
