// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Theming;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the window's own line icons: every one has an outline that draws, at the size and in the colour asked for.</summary>
public sealed class WindowIconTests
{
    /// <summary>The side of an icon beside text at a scale of one, in pixels.</summary>
    private const int Size = 20;

    /// <summary>A larger icon, as the start view draws.</summary>
    private const int LargeSize = 60;

    /// <summary>The middle row and column of the larger icon.</summary>
    private const int Middle = 30;

    /// <summary>A row and column of the larger icon a sixth of the way in from an edge.</summary>
    private const int NearEdge = 10;

    /// <summary>The colour the icons are drawn in.</summary>
    private const uint Ink = 0xFF8FA8B8;

    /// <summary>The fewest pixels a line icon covers at the small size.</summary>
    private const int FewestInkPixels = 12;

    /// <summary>The coverage of a pixel that counts as inked.</summary>
    private const byte Inked = 128;

    /// <summary>Every icon has an outline, and its strokes cover some of the square but never all of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryIconDrawsSomeInk()
    {
        var coverage = new CoverageBuffer();
        var empty = new List<ChromeIcon>();
        var solid = new List<ChromeIcon>();
        foreach (var icon in Enum.GetValues<ChromeIcon>())
        {
            if (icon == ChromeIcon.None)
            {
                continue;
            }

            IconRuns.Cover(coverage, icon, Size);
            var inked = CountInk(coverage);
            if (inked < FewestInkPixels)
            {
                empty.Add(icon);
            }

            if (inked == Size * Size)
            {
                solid.Add(icon);
            }
        }

        await Assert.That(empty).IsEmpty();
        await Assert.That(solid).IsEmpty();
    }

    /// <summary>No icon has no outline, so a control never asks for an icon that cannot be drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoIconDrawsNothing()
    {
        var coverage = new CoverageBuffer();
        IconRuns.Cover(coverage, ChromeIcon.None, Size);

        await Assert.That(CountInk(coverage)).IsEqualTo(0);
        await Assert.That(IconPaths.Of(ChromeIcon.None).IsEmpty).IsTrue();
        await Assert.That(IconPaths.Of(ChromeIcon.More).IsEmpty).IsFalse();
    }

    /// <summary>A straight line icon is stroked along its diagonal and leaves the other corners clear.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineIconFollowsItsDiagonal()
    {
        var coverage = new CoverageBuffer();
        IconRuns.Cover(coverage, ChromeIcon.Line, LargeSize);
        var row = new byte[LargeSize];

        // The line runs from the bottom left to the top right, so the middle is inked and the top left corner is not.
        coverage.ReadRow(Middle, row);
        var middle = row[Middle];
        coverage.ReadRow(NearEdge, row);
        var topLeft = row[NearEdge];
        var topRight = row[LargeSize - NearEdge - 1];

        await Assert.That(middle).IsGreaterThan(Inked);
        await Assert.That(topLeft).IsEqualTo((byte)0);
        await Assert.That(topRight).IsGreaterThan(Inked);
    }

    /// <summary>An oval is drawn from two arcs, and comes out the same on its left and right halves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OvalIconIsTheSameOnBothSides()
    {
        var coverage = new CoverageBuffer();
        IconRuns.Cover(coverage, ChromeIcon.Oval, LargeSize);
        var row = new byte[LargeSize];
        coverage.ReadRow(Middle, row);
        var left = 0;
        var right = 0;
        for (var x = 0; x < Middle; x++)
        {
            left += row[x] > Inked ? 1 : 0;
            right += row[LargeSize - 1 - x] > Inked ? 1 : 0;
        }

        // The ring is crossed once on each side of the middle row, and the middle of the oval is clear.
        await Assert.That(left).IsGreaterThan(0);
        await Assert.That(Math.Abs(left - right)).IsLessThanOrEqualTo(1);
        await Assert.That(row[Middle]).IsEqualTo((byte)0);
    }

    /// <summary>An icon is drawn once for a size and a colour, and the same surface is handed back after.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconIsDrawnOnceAndKept()
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        using var icons = new IconRuns(backend.Surfaces);
        var first = icons.Get(ChromeIcon.Save, Size, Ink);
        var again = icons.Get(ChromeIcon.Save, Size, Ink);
        var larger = icons.Get(ChromeIcon.Save, LargeSize, Ink);

        await Assert.That(first).IsNotNull();
        await Assert.That(ReferenceEquals(first, again)).IsTrue();
        await Assert.That(ReferenceEquals(first, larger)).IsFalse();
        await Assert.That(first!.Width).IsEqualTo(Size);
        await Assert.That(larger!.Height).IsEqualTo(LargeSize);
        await Assert.That(icons.Get(ChromeIcon.None, Size, Ink)).IsNull();
        await Assert.That(icons.Get(ChromeIcon.Save, 0, Ink)).IsNull();
    }

    /// <summary>Each icon is tinted by what its action does, and a theme without tints draws it in the text colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconsAreTintedByWhatTheyDo()
    {
        var theme = ChromeTheme.From(ColorSchemes.Calm);
        var plain = theme with { Tints = default };

        await Assert.That(IconPaths.ToneOf(ChromeIcon.Open)).IsEqualTo(IconTone.Add);
        await Assert.That(IconPaths.ToneOf(ChromeIcon.Search)).IsEqualTo(IconTone.Navigation);
        await Assert.That(IconPaths.ToneOf(ChromeIcon.Highlight)).IsEqualTo(IconTone.Edit);
        await Assert.That(IconPaths.ToneOf(ChromeIcon.Close)).IsEqualTo(IconTone.Remove);
        await Assert.That(IconPaths.ToneOf(ChromeIcon.Menu)).IsEqualTo(IconTone.Plain);
        await Assert.That(theme.IconColor(IconTone.Add)).IsEqualTo(0xFF000000 | ColorSchemes.CalmTints.Add);
        await Assert.That(theme.IconColor(IconTone.Remove)).IsEqualTo(0xFF000000 | ColorSchemes.CalmTints.Remove);
        await Assert.That(theme.IconColor(IconTone.Plain)).IsEqualTo(theme.Text);
        await Assert.That(plain.IconColor(IconTone.Navigation)).IsEqualTo(plain.Text);
    }

    /// <summary>Counts the pixels of a coverage buffer that are more than half covered.</summary>
    /// <param name="coverage">The buffer.</param>
    /// <returns>The count.</returns>
    private static int CountInk(CoverageBuffer coverage)
    {
        var row = new byte[coverage.Width];
        var count = 0;
        for (var y = 0; y < coverage.Height; y++)
        {
            coverage.ReadRow(y, row);
            foreach (var value in row)
            {
                count += value > Inked ? 1 : 0;
            }
        }

        return count;
    }
}
