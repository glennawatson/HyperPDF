// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Turns X11 key symbols, button numbers and state bits into the viewer's own input types.</summary>
internal static class X11Input
{
    /// <summary>The key symbol of Home.</summary>
    private const uint HomeSymbol = 0xFF50;

    /// <summary>The key symbol of the left arrow.</summary>
    private const uint LeftSymbol = 0xFF51;

    /// <summary>The key symbol of the up arrow.</summary>
    private const uint UpSymbol = 0xFF52;

    /// <summary>The key symbol of the right arrow.</summary>
    private const uint RightSymbol = 0xFF53;

    /// <summary>The key symbol of the down arrow.</summary>
    private const uint DownSymbol = 0xFF54;

    /// <summary>The key symbol of Page Up.</summary>
    private const uint PageUpSymbol = 0xFF55;

    /// <summary>The key symbol of Page Down.</summary>
    private const uint PageDownSymbol = 0xFF56;

    /// <summary>The key symbol of End.</summary>
    private const uint EndSymbol = 0xFF57;

    /// <summary>The key symbol of Return.</summary>
    private const uint ReturnSymbol = 0xFF0D;

    /// <summary>The key symbol of Enter on the number pad.</summary>
    private const uint PadEnterSymbol = 0xFF8D;

    /// <summary>The key symbol of Escape.</summary>
    private const uint EscapeSymbol = 0xFF1B;

    /// <summary>The key symbol of Delete.</summary>
    private const uint DeleteSymbol = 0xFFFF;

    /// <summary>The key symbol of Backspace.</summary>
    private const uint BackspaceSymbol = 0xFF08;

    /// <summary>The key symbol of Tab.</summary>
    private const uint TabSymbol = 0xFF09;

    /// <summary>The key symbol Shift and Tab make together.</summary>
    private const uint BackTabSymbol = 0xFE20;

    /// <summary>The key symbol of the space bar.</summary>
    private const uint SpaceSymbol = 0x20;

    /// <summary>The key symbol of F2.</summary>
    private const uint F2Symbol = 0xFFBF;

    /// <summary>The key symbol of the plus sign.</summary>
    private const uint PlusSymbol = 0x2B;

    /// <summary>The key symbol of the equals sign, which is the plus key without Shift.</summary>
    private const uint EqualsSymbol = 0x3D;

    /// <summary>The key symbol of plus on the number pad.</summary>
    private const uint PadAddSymbol = 0xFFAB;

    /// <summary>The key symbol of the minus sign.</summary>
    private const uint MinusSymbol = 0x2D;

    /// <summary>The key symbol of minus on the number pad.</summary>
    private const uint PadSubtractSymbol = 0xFFAD;

    /// <summary>The key symbol of the letter a.</summary>
    private const uint LetterA = 0x61;

    /// <summary>The key symbol of the letter c.</summary>
    private const uint LetterC = 0x63;

    /// <summary>The key symbol of the letter f.</summary>
    private const uint LetterF = 0x66;

    /// <summary>The key symbol of the letter o.</summary>
    private const uint LetterO = 0x6F;

    /// <summary>The key symbol of the letter s.</summary>
    private const uint LetterS = 0x73;

    /// <summary>The key symbol of the letter w.</summary>
    private const uint LetterW = 0x77;

    /// <summary>The key symbol of the letter v.</summary>
    private const uint LetterV = 0x76;

    /// <summary>The first key symbol that is a printing Latin-1 character and its own Unicode value.</summary>
    private const uint FirstPrinting = 0x20;

    /// <summary>The last printing ASCII key symbol.</summary>
    private const uint LastAscii = 0x7E;

    /// <summary>The first printing key symbol of the upper half of Latin-1.</summary>
    private const uint FirstUpperLatin = 0xA0;

    /// <summary>The last key symbol of Latin-1.</summary>
    private const uint LastLatin = 0xFF;

    /// <summary>The value added to a Unicode scalar to make its key symbol.</summary>
    private const uint UnicodeSymbolBase = 0x01000000;

    /// <summary>The largest Unicode key symbol.</summary>
    private const uint LastUnicodeSymbol = 0x0110FFFF;

    /// <summary>The bit that turns an upper case Latin letter symbol into its lower case symbol.</summary>
    private const uint LowerCaseBit = 0x20;

    /// <summary>The first upper case Latin letter symbol.</summary>
    private const uint FirstUpperCase = 0x41;

    /// <summary>The last upper case Latin letter symbol.</summary>
    private const uint LastUpperCase = 0x5A;

    /// <summary>The state bit of Shift.</summary>
    private const uint ShiftState = 1;

    /// <summary>The state bit of Control.</summary>
    private const uint ControlState = 1 << 2;

    /// <summary>The state bit of Alt, which X11 calls Mod1.</summary>
    private const uint AltState = 1 << 3;

    /// <summary>The state bit of the Meta or logo key, which X11 calls Mod4.</summary>
    private const uint MetaState = 1 << 6;

    /// <summary>The state bit of the left button.</summary>
    private const uint LeftButtonState = 1 << 8;

    /// <summary>The state bit of the middle button.</summary>
    private const uint MiddleButtonState = 1 << 9;

    /// <summary>The state bit of the right button.</summary>
    private const uint RightButtonState = 1 << 10;

    /// <summary>The button number of the left button.</summary>
    private const uint LeftButton = 1;

    /// <summary>The button number of the middle button.</summary>
    private const uint MiddleButton = 2;

    /// <summary>The button number of the right button.</summary>
    private const uint RightButton = 3;

    /// <summary>The button number the wheel reports when it turns up.</summary>
    private const uint WheelUp = 4;

    /// <summary>The button number the wheel reports when it turns down.</summary>
    private const uint WheelDown = 5;

    /// <summary>The button number the wheel reports when it tilts left.</summary>
    private const uint WheelLeft = 6;

    /// <summary>The button number the wheel reports when it tilts right.</summary>
    private const uint WheelRight = 7;

    /// <summary>Finds the viewer key of a key symbol.</summary>
    /// <param name="symbol">The key symbol.</param>
    /// <returns>The key, or <see cref="ViewerKey.Other"/> for a key the viewer has no use for.</returns>
    internal static ViewerKey ToKey(uint symbol)
    {
        var lower = symbol is >= FirstUpperCase and <= LastUpperCase ? symbol | LowerCaseBit : symbol;
        return lower switch
        {
            LeftSymbol => ViewerKey.Left,
            RightSymbol => ViewerKey.Right,
            UpSymbol => ViewerKey.Up,
            DownSymbol => ViewerKey.Down,
            PageUpSymbol => ViewerKey.PageUp,
            PageDownSymbol => ViewerKey.PageDown,
            HomeSymbol => ViewerKey.Home,
            EndSymbol => ViewerKey.End,
            _ => ToEditKey(lower),
        };
    }

    /// <summary>Finds the character a key symbol types. Latin-1 symbols and Unicode symbols are covered; the older symbols of other scripts are not.</summary>
    /// <param name="symbol">The key symbol with Shift applied.</param>
    /// <returns>The Unicode scalar value, or 0 when the symbol types nothing the viewer can use.</returns>
    internal static uint ToScalar(uint symbol) => symbol switch
    {
        (>= FirstPrinting and <= LastAscii) or (>= FirstUpperLatin and <= LastLatin) => symbol,
        > UnicodeSymbolBase and <= LastUnicodeSymbol => symbol - UnicodeSymbolBase,
        _ => 0,
    };

    /// <summary>Turns X11 state bits into the modifier keys held.</summary>
    /// <param name="state">The state bits of an input event.</param>
    /// <returns>The modifiers.</returns>
    internal static ViewerModifiers ToModifiers(uint state) =>
        ((state & ShiftState) != 0 ? ViewerModifiers.Shift : ViewerModifiers.None)
        | ((state & ControlState) != 0 ? ViewerModifiers.Control : ViewerModifiers.None)
        | ((state & AltState) != 0 ? ViewerModifiers.Alt : ViewerModifiers.None)
        | ((state & MetaState) != 0 ? ViewerModifiers.Meta : ViewerModifiers.None);

    /// <summary>Turns X11 state bits into the pointer buttons held.</summary>
    /// <param name="state">The state bits of an input event.</param>
    /// <returns>The buttons.</returns>
    internal static ViewerButtons ToHeldButtons(uint state) =>
        ((state & LeftButtonState) != 0 ? ViewerButtons.Left : ViewerButtons.None)
        | ((state & MiddleButtonState) != 0 ? ViewerButtons.Middle : ViewerButtons.None)
        | ((state & RightButtonState) != 0 ? ViewerButtons.Right : ViewerButtons.None);

    /// <summary>Turns an X11 button number into a pointer button.</summary>
    /// <param name="button">The button number.</param>
    /// <returns>The button, or <see cref="ViewerButtons.None"/> for a wheel or extra button.</returns>
    internal static ViewerButtons ToButton(uint button) => button switch
    {
        LeftButton => ViewerButtons.Left,
        MiddleButton => ViewerButtons.Middle,
        RightButton => ViewerButtons.Right,
        _ => ViewerButtons.None,
    };

    /// <summary>Finds the scroll a wheel button stands for.</summary>
    /// <param name="button">The button number.</param>
    /// <returns>The scroll in notch units, sideways and then up and down, or the default when the button is not a wheel.</returns>
    internal static WheelStep ToWheel(uint button) => button switch
    {
        WheelUp => new(0, Windowing.Windows.WindowEvent.ScrollNotch),
        WheelDown => new(0, -Windowing.Windows.WindowEvent.ScrollNotch),
        WheelLeft => new(Windowing.Windows.WindowEvent.ScrollNotch, 0),
        WheelRight => new(-Windowing.Windows.WindowEvent.ScrollNotch, 0),
        _ => default,
    };

    /// <summary>Finds the viewer key of a symbol that is not a navigation key.</summary>
    /// <param name="symbol">The key symbol, with Latin letters in lower case.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToEditKey(uint symbol) => symbol switch
    {
        ReturnSymbol or PadEnterSymbol => ViewerKey.Enter,
        EscapeSymbol => ViewerKey.Escape,
        DeleteSymbol => ViewerKey.Delete,
        BackspaceSymbol => ViewerKey.Back,
        TabSymbol or BackTabSymbol => ViewerKey.Tab,
        SpaceSymbol => ViewerKey.Space,
        F2Symbol => ViewerKey.F2,
        PlusSymbol or EqualsSymbol or PadAddSymbol => ViewerKey.Plus,
        MinusSymbol or PadSubtractSymbol => ViewerKey.Minus,
        _ => ToLetterKey(symbol),
    };

    /// <summary>Finds the viewer key of a letter the viewer has a shortcut for.</summary>
    /// <param name="symbol">The key symbol, with Latin letters in lower case.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToLetterKey(uint symbol) => symbol switch
    {
        LetterA => ViewerKey.A,
        LetterC => ViewerKey.C,
        LetterF => ViewerKey.F,
        LetterO => ViewerKey.O,
        LetterS => ViewerKey.S,
        LetterW => ViewerKey.W,
        LetterV => ViewerKey.V,
        _ => ViewerKey.Other,
    };
}
