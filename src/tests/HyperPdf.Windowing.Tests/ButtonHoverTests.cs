// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.ViewModels.Help;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks button feedback on the CPU renderer and the delayed help lifecycle.</summary>
public sealed class ButtonHoverTests
{
    /// <summary>The square test window size.</summary>
    private const int Size = 128;

    /// <summary>The hovered button fill.</summary>
    private const uint Fill = 0xFF778899;

    /// <summary>The hover outline.</summary>
    private const uint Outline = 0xFF123456;

    /// <summary>The pressed outline.</summary>
    private const uint Accent = 0xFFABCDEF;

    /// <summary>A labelled button shows a fill on entry, an accent when held and clears on exit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoverAndPressHaveDistinctVisibleStates()
    {
        using var window = new HeadlessWindow(Size, Size);
        using var runs = new TextRuns(window.Backend.Surfaces);
        var theme = new ChromeTheme(0, 0, Outline, 0, Outline, Accent, 0, 0) { ButtonHover = Fill };
        var look = new ChromeLook(runs, theme, ChromeMetrics.For(1), static _ => null, static () => null);
        var button = new ChromeItem(ChromeItemKind.Button, OverlayAction.Accept, 0, "Open", new(0, 0, Size, Size), true, false);
        var pointer = new ChromePointer(window);
        _ = pointer.Move(button);
        using (var pass = window.Backend.BeginFrame())
        {
            pointer.DrawHover(pass, look);
        }

        var hovered = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels)[0];
        var interior = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels)[Size + 1];
        pointer.Press(true);
        using (var pass = window.Backend.BeginFrame())
        {
            pointer.DrawHover(pass, look);
        }

        var pressed = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels)[0];
        pointer.Leave();
        await Assert.That(hovered).IsEqualTo(Outline);
        await Assert.That(interior).IsEqualTo(Fill);
        await Assert.That(pressed).IsEqualTo(Accent);
        await Assert.That(pointer.Hover).IsNull();
    }

    /// <summary>Disabled buttons can explain their action and cannot be activated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisabledButtonOffersHelpAfterTheDeadline()
    {
        var button = new ChromeItem(ChromeItemKind.Button, OverlayAction.Accept, 0, "Open", new(0, 0, Size, Size), false, false) { Help = Descriptions.Open };
        List<ChromeItem> buttons = [button];
        var tooltip = new ButtonTooltip();
        using var window = new HeadlessWindow(Size, Size);
        var pointer = new ChromePointer(window);
        pointer.SetTargets(buttons, false);
        tooltip.Enter(pointer.Hit(false, 1, 1));
        var due = tooltip.Due;
        await Assert.That(ChromePointer.Find(buttons, 1, 1)).IsNull();
        await Assert.That(tooltip.OpenIfDue(due - 1)).IsFalse();
        await Assert.That(tooltip.OpenIfDue(due)).IsTrue();
        tooltip.Enter(null);
        await Assert.That(tooltip.Visible).IsFalse();
        await Assert.That(tooltip.Due).IsEqualTo(0L);
    }
}
