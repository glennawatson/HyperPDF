// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Cpu;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the widths and the cut names a layout keeps between builds are the ones it would measure and cut afresh.</summary>
public sealed class KeptTextWidthTests
{
    /// <summary>The text size in pixels.</summary>
    private const int Size = 14;

    /// <summary>The text colour.</summary>
    private const uint Ink = 0xFF000000;

    /// <summary>A width too narrow for the name, so it is cut.</summary>
    private const int NarrowWidth = 90;

    /// <summary>A name longer than a tab is wide.</summary>
    private const string Name = "Annual report and accounts for the year.pdf";

    /// <summary>A kept width is the measured width, the first time it is asked for and after.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Bookmarks")]
    [Arguments(Name)]
    [Arguments("文档书签")]
    [Arguments("")]
    public async Task KeptWidthIsTheMeasuredWidth(string text)
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        using var runs = new TextRuns(backend.Surfaces);
        var style = new TextStyle(Size, false, Ink);
        var measured = runs.Measure(text, style);

        await Assert.That(runs.Width(text, style)).IsEqualTo(measured);
        await Assert.That(runs.Width(text, style)).IsEqualTo(measured);
        await Assert.That(runs.Width(text, style with { Bold = true })).IsEqualTo(runs.Measure(text, style with { Bold = true }));
    }

    /// <summary>A name cut once is cut the same way the next time, and a different width or style is cut for itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptCutIsTheFreshCut()
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        using var runs = new TextRuns(backend.Surfaces);
        using var fresh = new TextRuns(backend.Surfaces);
        var style = new TextStyle(Size, false, Ink);
        var first = ChromeText.Fit(Name, style, NarrowWidth, runs);
        var again = ChromeText.Fit(Name, style, NarrowWidth, runs);
        var wider = ChromeText.Fit(Name, style, NarrowWidth + NarrowWidth, runs);
        var bold = ChromeText.Fit(Name, style with { Bold = true }, NarrowWidth, runs);

        await Assert.That(first).IsNotEqualTo(Name);
        await Assert.That(runs.Measure(first, style)).IsLessThanOrEqualTo(NarrowWidth);
        await Assert.That(again).IsSameReferenceAs(first);
        await Assert.That(wider).IsEqualTo(ChromeText.Fit(Name, style, NarrowWidth + NarrowWidth, fresh));
        await Assert.That(wider.Length).IsGreaterThan(first.Length);
        await Assert.That(bold).IsEqualTo(ChromeText.Fit(Name, style with { Bold = true }, NarrowWidth, fresh));
    }
}
