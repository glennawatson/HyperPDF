// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the window's own text: labels in the bundled font, and labels in scripts it lacks, which a system font draws.</summary>
public sealed class WindowTextTests
{
    /// <summary>The text size in pixels.</summary>
    private const int Size = 20;

    /// <summary>The text colour.</summary>
    private const uint Ink = 0xFF000000;

    /// <summary>The fewest pixels a real word covers. A row of empty boxes for missing glyphs covers fewer than a word of this length.</summary>
    private const int FewestInkPixels = 40;

    /// <summary>A Latin label is drawn with ink inside it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LatinLabelIsDrawn()
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        using var runs = new TextRuns(backend.Surfaces);
        var run = runs.Get("Bookmarks", new(Size, false, Ink));

        await Assert.That(run.Width).IsGreaterThan(Size);
        await Assert.That(CountInk(run)).IsGreaterThan(FewestInkPixels);
    }

    /// <summary>Chinese, Arabic and Hebrew labels are drawn from a system font, and their glyphs differ from the bundled font's missing glyph.</summary>
    /// <param name="text">A word in a script the bundled font lacks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("文档书签")]
    [Arguments("مستند")]
    [Arguments("מסמך")]
    [Arguments("เอกสาร")]
    [Arguments("दस्तावेज़")]
    [Arguments("문서 열기")]
    public async Task OtherScriptsAreDrawnFromASystemFont(string text)
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        using var runs = new TextRuns(backend.Surfaces);
        var style = new TextStyle(Size, false, Ink);
        var run = runs.Get(text, style);

        // A character with no font at all is drawn as the bundled font's missing glyph, the same box each time.
        var boxes = runs.Get(string.Concat(Enumerable.Repeat("", text.Length)), style);
        if (Same(run, boxes) && CountInk(run) == CountInk(boxes))
        {
            Skip.Test("This machine has no font for the script.");
            return;
        }

        await Assert.That(run.Width).IsGreaterThan(0);
        await Assert.That(CountInk(run)).IsGreaterThan(FewestInkPixels);
        await Assert.That(runs.Measure(text, style)).IsGreaterThan(0);
    }

    /// <summary>Counts the pixels of a drawn label that have any ink.</summary>
    /// <param name="run">The label.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountInk(TextRun run)
    {
        var count = 0;
        _ = run.Surface?.Write(0, (RenderTarget target, in int _) =>
        {
            foreach (var pixel in MemoryMarshal.Cast<byte, uint>(target.Pixels))
            {
                count += pixel != 0 ? 1 : 0;
            }

            return true;
        });
        return count;
    }

    /// <summary>Tells whether two labels are the same size, which two different words of real text are not.</summary>
    /// <param name="left">One label.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> when both have the same width and height.</returns>
    private static bool Same(TextRun left, TextRun right) => left.Width == right.Width && left.Height == right.Height;
}
