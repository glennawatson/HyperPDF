// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Turns Windows virtual key codes and mouse key state into the viewer's own input types.</summary>
internal static class Win32Input
{
    /// <summary>The virtual key code of Backspace.</summary>
    private const uint BackKey = 0x08;

    /// <summary>The virtual key code of Tab.</summary>
    private const uint TabKey = 0x09;

    /// <summary>The virtual key code of Enter.</summary>
    private const uint ReturnKey = 0x0D;

    /// <summary>The virtual key code of Escape.</summary>
    private const uint EscapeKey = 0x1B;

    /// <summary>The virtual key code of the space bar.</summary>
    private const uint SpaceKey = 0x20;

    /// <summary>The virtual key code of Page Up.</summary>
    private const uint PriorKey = 0x21;

    /// <summary>The virtual key code of Page Down.</summary>
    private const uint NextKey = 0x22;

    /// <summary>The virtual key code of End.</summary>
    private const uint EndKey = 0x23;

    /// <summary>The virtual key code of Home.</summary>
    private const uint HomeKey = 0x24;

    /// <summary>The virtual key code of the left arrow.</summary>
    private const uint LeftKey = 0x25;

    /// <summary>The virtual key code of the up arrow.</summary>
    private const uint UpKey = 0x26;

    /// <summary>The virtual key code of the right arrow.</summary>
    private const uint RightKey = 0x27;

    /// <summary>The virtual key code of the down arrow.</summary>
    private const uint DownKey = 0x28;

    /// <summary>The virtual key code of Delete.</summary>
    private const uint DeleteKey = 0x2E;

    /// <summary>The virtual key code of the A key.</summary>
    private const uint LetterA = 0x41;

    /// <summary>The virtual key code of the C key.</summary>
    private const uint LetterC = 0x43;

    /// <summary>The virtual key code of the F key.</summary>
    private const uint LetterF = 0x46;

    /// <summary>The virtual key code of the O key.</summary>
    private const uint LetterO = 0x4F;

    /// <summary>The virtual key code of the S key.</summary>
    private const uint LetterS = 0x53;

    /// <summary>The virtual key code of the W key.</summary>
    private const uint LetterW = 0x57;

    /// <summary>The virtual key code of plus on the number pad.</summary>
    private const uint AddKey = 0x6B;

    /// <summary>The virtual key code of minus on the number pad.</summary>
    private const uint SubtractKey = 0x6D;

    /// <summary>The virtual key code of F2.</summary>
    private const uint F2Key = 0x71;

    /// <summary>The virtual key code of the plus key on any layout.</summary>
    private const uint OemPlusKey = 0xBB;

    /// <summary>The virtual key code of the minus key on any layout.</summary>
    private const uint OemMinusKey = 0xBD;

    /// <summary>The mouse key state bit of the left button.</summary>
    private const nuint LeftHeld = 0x01;

    /// <summary>The mouse key state bit of the right button.</summary>
    private const nuint RightHeld = 0x02;

    /// <summary>The mouse key state bit of the middle button.</summary>
    private const nuint MiddleHeld = 0x10;

    /// <summary>Finds the viewer key of a virtual key code.</summary>
    /// <param name="virtualKey">The virtual key code.</param>
    /// <returns>The key, or <see cref="ViewerKey.Other"/> for a key the viewer has no use for.</returns>
    internal static ViewerKey ToKey(uint virtualKey) => virtualKey switch
    {
        LeftKey => ViewerKey.Left,
        RightKey => ViewerKey.Right,
        UpKey => ViewerKey.Up,
        DownKey => ViewerKey.Down,
        PriorKey => ViewerKey.PageUp,
        NextKey => ViewerKey.PageDown,
        HomeKey => ViewerKey.Home,
        EndKey => ViewerKey.End,
        _ => ToEditKey(virtualKey),
    };

    /// <summary>Turns the mouse key state of a mouse message into the buttons held.</summary>
    /// <param name="keyState">The first parameter of the message.</param>
    /// <returns>The buttons.</returns>
    internal static ViewerButtons ToHeldButtons(nuint keyState) =>
        ((keyState & LeftHeld) != 0 ? ViewerButtons.Left : ViewerButtons.None)
        | ((keyState & MiddleHeld) != 0 ? ViewerButtons.Middle : ViewerButtons.None)
        | ((keyState & RightHeld) != 0 ? ViewerButtons.Right : ViewerButtons.None);

    /// <summary>Finds the viewer key of a code that is not a navigation key.</summary>
    /// <param name="virtualKey">The virtual key code.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToEditKey(uint virtualKey) => virtualKey switch
    {
        ReturnKey => ViewerKey.Enter,
        EscapeKey => ViewerKey.Escape,
        DeleteKey => ViewerKey.Delete,
        BackKey => ViewerKey.Back,
        TabKey => ViewerKey.Tab,
        SpaceKey => ViewerKey.Space,
        F2Key => ViewerKey.F2,
        OemPlusKey or AddKey => ViewerKey.Plus,
        OemMinusKey or SubtractKey => ViewerKey.Minus,
        _ => ToLetterKey(virtualKey),
    };

    /// <summary>Finds the viewer key of a letter the viewer has a shortcut for.</summary>
    /// <param name="key">The virtual key code.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToLetterKey(uint key) => key switch
    {
        LetterA => ViewerKey.A,
        LetterC => ViewerKey.C,
        LetterF => ViewerKey.F,
        LetterO => ViewerKey.O,
        LetterS => ViewerKey.S,
        LetterW => ViewerKey.W,
        _ => ViewerKey.Other,
    };
}
