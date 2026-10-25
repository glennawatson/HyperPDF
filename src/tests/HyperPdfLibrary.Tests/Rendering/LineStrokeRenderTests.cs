// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that stroking one straight segment gives the same pixels as stroking an equal path that is not recognised as one segment.</summary>
[NotInParallel]
public sealed class LineStrokeRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The segment, with fractional ends so edge pixels carry partial coverage.</summary>
    private const string Segment = "10.3 12.7 m 90.45 60.2 l";

    /// <summary>The same segment with its end repeated, which Skia's stroker skips but which is three commands to the recogniser.</summary>
    private const string RepeatedEnd = "10.3 12.7 m 90.45 60.2 l 90.45 60.2 l";

    /// <summary>Strokes of different widths, caps and dashes give the same pixels as the equal path.</summary>
    /// <param name="style">The line style operators.</param>
    /// <param name="turns">The quarter turns the page is rendered at.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1 w", 0)]
    [Arguments("0.2 w", 0)]
    [Arguments("0 w", 0)]
    [Arguments("5.3 w 1 J", 0)]
    [Arguments("7 w 2 J", 0)]
    [Arguments("9 w [4 3] 0 d", 0)]
    [Arguments("3 w 1 0 0 RG", 0)]
    [Arguments("1 w", 1)]
    [Arguments("6.5 w 1 J", 1)]
    public async Task SegmentStrokeMatchesEqualPathStroke(string style, int turns)
    {
        var segment = Render($"{style} {Segment} S", turns);
        var path = Render($"{style} {RepeatedEnd} S", turns);

        await Assert.That(path.Pixels).IsEquivalentTo(segment.Pixels);
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
