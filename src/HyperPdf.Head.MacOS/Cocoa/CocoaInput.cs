// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>
/// Turns macOS virtual key codes and modifier flags into the viewer's own input types. The key codes name key positions, so the
/// letter and sign keys are found where a US layout has them; the navigation keys are the same on every layout.
/// </summary>
internal static class CocoaInput
{
    /// <summary>The key code of the A key.</summary>
    private const ushort LetterA = 0;

    /// <summary>The key code of the C key.</summary>
    private const ushort LetterC = 8;

    /// <summary>The key code of the F key.</summary>
    private const ushort LetterF = 3;

    /// <summary>The key code of the O key.</summary>
    private const ushort LetterO = 31;

    /// <summary>The key code of the S key.</summary>
    private const ushort LetterS = 1;

    /// <summary>The key code of the W key.</summary>
    private const ushort LetterW = 13;

    /// <summary>The key code of the equals key, which is plus with Shift.</summary>
    private const ushort EqualsKey = 24;

    /// <summary>The key code of the minus key.</summary>
    private const ushort MinusKey = 27;

    /// <summary>The key code of Return.</summary>
    private const ushort ReturnKey = 36;

    /// <summary>The key code of Tab.</summary>
    private const ushort TabKey = 48;

    /// <summary>The key code of the space bar.</summary>
    private const ushort SpaceKey = 49;

    /// <summary>The key code of Delete, which removes the character before the caret.</summary>
    private const ushort BackspaceKey = 51;

    /// <summary>The key code of Escape.</summary>
    private const ushort EscapeKey = 53;

    /// <summary>The key code of plus on the number pad.</summary>
    private const ushort PadPlusKey = 69;

    /// <summary>The key code of Enter on the number pad.</summary>
    private const ushort PadEnterKey = 76;

    /// <summary>The key code of minus on the number pad.</summary>
    private const ushort PadMinusKey = 78;

    /// <summary>The key code of Home.</summary>
    private const ushort HomeKey = 115;

    /// <summary>The key code of Page Up.</summary>
    private const ushort PageUpKey = 116;

    /// <summary>The key code of Forward Delete.</summary>
    private const ushort ForwardDeleteKey = 117;

    /// <summary>The key code of End.</summary>
    private const ushort EndKey = 119;

    /// <summary>The key code of F2.</summary>
    private const ushort F2Key = 120;

    /// <summary>The key code of Page Down.</summary>
    private const ushort PageDownKey = 121;

    /// <summary>The key code of the left arrow.</summary>
    private const ushort LeftKey = 123;

    /// <summary>The key code of the right arrow.</summary>
    private const ushort RightKey = 124;

    /// <summary>The key code of the down arrow.</summary>
    private const ushort DownKey = 125;

    /// <summary>The key code of the up arrow.</summary>
    private const ushort UpKey = 126;

    /// <summary>The modifier flag of Shift.</summary>
    private const ulong ShiftFlag = 1 << 17;

    /// <summary>The modifier flag of Control.</summary>
    private const ulong ControlFlag = 1 << 18;

    /// <summary>The modifier flag of Option.</summary>
    private const ulong OptionFlag = 1 << 19;

    /// <summary>The modifier flag of Command.</summary>
    private const ulong CommandFlag = 1 << 20;

    /// <summary>Finds the viewer key of a key code.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <returns>The key, or <see cref="ViewerKey.Other"/> for a key the viewer has no use for.</returns>
    internal static ViewerKey ToKey(ushort keyCode) => keyCode switch
    {
        LeftKey => ViewerKey.Left,
        RightKey => ViewerKey.Right,
        UpKey => ViewerKey.Up,
        DownKey => ViewerKey.Down,
        PageUpKey => ViewerKey.PageUp,
        PageDownKey => ViewerKey.PageDown,
        HomeKey => ViewerKey.Home,
        EndKey => ViewerKey.End,
        _ => ToEditKey(keyCode),
    };

    /// <summary>
    /// Turns modifier flags into the modifier keys held. Command does on a Mac what Control does elsewhere, such as copying and
    /// zooming, so Command is reported as Control and the Mac's own Control key as Meta.
    /// </summary>
    /// <param name="flags">The modifier flags of an event.</param>
    /// <returns>The modifiers.</returns>
    internal static ViewerModifiers ToModifiers(ulong flags) =>
        ((flags & ShiftFlag) != 0 ? ViewerModifiers.Shift : ViewerModifiers.None)
        | ((flags & CommandFlag) != 0 ? ViewerModifiers.Control : ViewerModifiers.None)
        | ((flags & OptionFlag) != 0 ? ViewerModifiers.Alt : ViewerModifiers.None)
        | ((flags & ControlFlag) != 0 ? ViewerModifiers.Meta : ViewerModifiers.None);

    /// <summary>Finds the viewer key of a code that is not a navigation key.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToEditKey(ushort keyCode) => keyCode switch
    {
        ReturnKey or PadEnterKey => ViewerKey.Enter,
        EscapeKey => ViewerKey.Escape,
        ForwardDeleteKey => ViewerKey.Delete,
        BackspaceKey => ViewerKey.Back,
        TabKey => ViewerKey.Tab,
        SpaceKey => ViewerKey.Space,
        F2Key => ViewerKey.F2,
        EqualsKey or PadPlusKey => ViewerKey.Plus,
        MinusKey or PadMinusKey => ViewerKey.Minus,
        _ => ToLetterKey(keyCode),
    };

    /// <summary>Finds the viewer key of a letter the viewer has a shortcut for.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToLetterKey(ushort keyCode) => keyCode switch
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
