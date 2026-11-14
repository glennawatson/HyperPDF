// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks how a display's scale is worked out and how the window's controls grow with it.</summary>
public sealed class DisplayScaleTests
{
    /// <summary>The dots per inch of a display at twice the usual scale.</summary>
    private const double DoubleDpi = 192;

    /// <summary>The scale of that display.</summary>
    private const double Double = 2;

    /// <summary>A scale a user might ask for.</summary>
    private const double OneAndHalf = 1.5;

    /// <summary>The largest scale the viewer uses.</summary>
    private const double Largest = 8;

    /// <summary>A scale far past anything a display has.</summary>
    private const double TooLarge = 1000;

    /// <summary>A display's dots per inch become its scale, and a display that reports none gets a scale of one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DotsPerInchBecomeAScale()
    {
        if (Environment.GetEnvironmentVariable(DisplayScale.OverrideVariable) is not null)
        {
            Skip.Test("The scale is set by hand on this machine, which replaces what a display reports.");
            return;
        }

        await Assert.That(DisplayScale.FromDpi(DoubleDpi)).IsEqualTo(Double);
        await Assert.That(DisplayScale.FromDpi(0)).IsEqualTo(1);
        await Assert.That(DisplayScale.Resolve(TooLarge)).IsEqualTo(Largest);
    }

    /// <summary>A scale typed by the user is read as a plain number, and anything else is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TypedScaleIsRead()
    {
        var read = DisplayScale.TryParse("1.5", out var scale);

        await Assert.That(read).IsTrue();
        await Assert.That(scale).IsEqualTo(OneAndHalf);
        await Assert.That(DisplayScale.TryParse("big", out _)).IsFalse();
        await Assert.That(DisplayScale.TryParse("-2", out _)).IsFalse();
        await Assert.That(DisplayScale.TryParse(null, out _)).IsFalse();
    }

    /// <summary>The window's controls take twice the pixels on a display at twice the scale, so they stay the same size on screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsGrowWithTheScale()
    {
        var plain = ChromeMetrics.For(1);
        var scaled = ChromeMetrics.For(Double);

        await Assert.That(scaled.ToolbarHeight).IsEqualTo(plain.ToolbarHeight * (int)Double);
        await Assert.That(scaled.TextSize).IsEqualTo(plain.TextSize * (int)Double);
        await Assert.That(scaled.RowHeight).IsEqualTo(plain.RowHeight * (int)Double);
    }
}
