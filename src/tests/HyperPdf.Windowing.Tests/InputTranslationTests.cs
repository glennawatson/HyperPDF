// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux.Wayland;
using HyperPdf.Head.Linux.X11;
using HyperPdf.Head.Windows.Win32;
using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that every head turns its own key, button, modifier and wheel numbers into the same viewer input.</summary>
public sealed class InputTranslationTests
{
    /// <summary>The X11 key symbol of Page Down.</summary>
    private const uint X11PageDown = 0xFF56;

    /// <summary>The X11 key symbol of an upper case C.</summary>
    private const uint X11UpperC = 0x43;

    /// <summary>The X11 state of Control with Shift and the left button.</summary>
    private const uint X11ControlShiftLeft = 0x105;

    /// <summary>The X11 button the wheel reports when it turns down.</summary>
    private const uint X11WheelDown = 5;

    /// <summary>The Linux input code of Page Down.</summary>
    private const uint EvdevPageDown = 109;

    /// <summary>The Linux input code of the right pointer button.</summary>
    private const uint EvdevRightButton = 0x111;

    /// <summary>The axis amount a compositor reports for one wheel notch down.</summary>
    private const double WaylandNotchDown = 15;

    /// <summary>The Windows virtual key code of Page Down.</summary>
    private const uint Win32PageDown = 0x22;

    /// <summary>The Windows virtual key code of the plus key.</summary>
    private const uint Win32OemPlus = 0xBB;

    /// <summary>The Windows mouse key state of the left and middle buttons.</summary>
    private const nuint Win32LeftAndMiddle = 0x11;

    /// <summary>A code no head maps.</summary>
    private const uint Unknown = 0x7FFF;

    /// <summary>X11 and Wayland agree on a navigation key, and a key the viewer has no use for is reported as other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinuxHeadsFindNavigationKeys()
    {
        await Assert.That(X11Input.ToKey(X11PageDown)).IsEqualTo(ViewerKey.PageDown);
        await Assert.That(EvdevInput.ToKey(EvdevPageDown)).IsEqualTo(ViewerKey.PageDown);
        await Assert.That(X11Input.ToKey(Unknown)).IsEqualTo(ViewerKey.Other);
        await Assert.That(EvdevInput.ToKey(Unknown)).IsEqualTo(ViewerKey.Other);
    }

    /// <summary>Letters are found in either case on X11.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task X11LettersAreFoundInEitherCase() => await Assert.That(X11Input.ToKey(X11UpperC)).IsEqualTo(ViewerKey.C);

    /// <summary>X11 state bits become the modifiers and the buttons held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task X11StateSplitsIntoModifiersAndButtons()
    {
        await Assert.That(X11Input.ToModifiers(X11ControlShiftLeft)).IsEqualTo(ViewerModifiers.Control | ViewerModifiers.Shift);
        await Assert.That(X11Input.ToHeldButtons(X11ControlShiftLeft)).IsEqualTo(ViewerButtons.Left);
    }

    /// <summary>A Wayland button code becomes a viewer button.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WaylandButtonsAreTranslated() => await Assert.That(EvdevInput.ToButton(EvdevRightButton)).IsEqualTo(ViewerButtons.Right);

    /// <summary>A wheel notch down is one negative notch on X11 and Wayland.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelDownIsANegativeNotch()
    {
        await Assert.That(X11Input.ToWheel(X11WheelDown).Upward).IsEqualTo(-WindowEvent.ScrollNotch);
        await Assert.That(EvdevInput.ToScroll(WaylandNotchDown)).IsEqualTo(-WindowEvent.ScrollNotch);
    }

    /// <summary>Windows virtual keys and mouse key state become viewer keys and buttons.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowsKeysAndButtonsAreTranslated()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("The Windows head only runs on Windows.");
            return;
        }

        await Assert.That(Win32Input.ToKey(Win32PageDown)).IsEqualTo(ViewerKey.PageDown);
        await Assert.That(Win32Input.ToKey(Win32OemPlus)).IsEqualTo(ViewerKey.Plus);
        await Assert.That(Win32Input.ToKey(Unknown)).IsEqualTo(ViewerKey.Other);
        await Assert.That(Win32Input.ToHeldButtons(Win32LeftAndMiddle)).IsEqualTo(ViewerButtons.Left | ViewerButtons.Middle);
    }
}
