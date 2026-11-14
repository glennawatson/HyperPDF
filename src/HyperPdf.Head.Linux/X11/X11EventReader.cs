// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Reads the fields of an XEvent from its raw bytes on 64 bit Linux and translates them into the viewer's input types.</summary>
internal static unsafe class X11EventReader
{
    /// <summary>The XEvent type of a key press.</summary>
    private const int KeyPressType = 2;

    /// <summary>The XEvent type of a key release.</summary>
    private const int KeyReleaseType = 3;

    /// <summary>The XEvent type of a button press.</summary>
    private const int ButtonPressType = 4;

    /// <summary>The XEvent type of a button release.</summary>
    private const int ButtonReleaseType = 5;

    /// <summary>The XEvent type of a pointer move.</summary>
    private const int MotionNotifyType = 6;

    /// <summary>The XEvent type of an expose.</summary>
    private const int ExposeType = 12;

    /// <summary>The XEvent type of a size or position change.</summary>
    private const int ConfigureNotifyType = 22;

    /// <summary>The XEvent type of a client message.</summary>
    private const int ClientMessageType = 33;

    /// <summary>The offset of the width in XConfigureEvent.</summary>
    private const int ConfigureWidthOffset = 56;

    /// <summary>The offset of the height in XConfigureEvent.</summary>
    private const int ConfigureHeightOffset = 60;

    /// <summary>The offset of the first data value in XClientMessageEvent.</summary>
    private const int MessageDataOffset = 56;

    /// <summary>The offset of x in the key, button and motion events.</summary>
    private const int InputXOffset = 64;

    /// <summary>The offset of y in the key, button and motion events.</summary>
    private const int InputYOffset = 68;

    /// <summary>The offset of the modifier and button state in the key, button and motion events.</summary>
    private const int InputStateOffset = 80;

    /// <summary>The offset of the key code or button number in the key and button events.</summary>
    private const int InputDetailOffset = 84;

    /// <summary>The bytes Xlib may write for the characters of one key.</summary>
    private const int TextBuffer = 16;

    /// <summary>Reads an XEvent into a window event.</summary>
    /// <param name="raw">The XEvent bytes.</param>
    /// <param name="deleteAtom">The WM_DELETE_WINDOW atom.</param>
    /// <param name="windowEvent">Receives the event.</param>
    /// <returns><see langword="true"/> when the event is one the viewer handles.</returns>
    internal static bool TryRead(byte* raw, nint deleteAtom, out WindowEvent windowEvent)
    {
        var type = *(int*)raw;
        windowEvent = type switch
        {
            ClientMessageType when *(nint*)(raw + MessageDataOffset) == deleteAtom => new(WindowEventKind.CloseRequested, 0, 0, 0, 0),
            ConfigureNotifyType => new(WindowEventKind.Resized, *(int*)(raw + ConfigureWidthOffset), *(int*)(raw + ConfigureHeightOffset), 0, 0),
            ExposeType => new(WindowEventKind.Exposed, 0, 0, 0, 0),
            KeyPressType => ReadKey(WindowEventKind.KeyDown, raw),
            KeyReleaseType => ReadKey(WindowEventKind.KeyUp, raw),
            ButtonPressType => ReadButton(raw, pressed: true),
            ButtonReleaseType => ReadButton(raw, pressed: false),
            MotionNotifyType => ReadMotion(raw),
            _ => default,
        };
        return windowEvent != default;
    }

    /// <summary>Reads the character a key press types, when it types one.</summary>
    /// <param name="raw">The XEvent bytes of a key press.</param>
    /// <param name="typed">Receives the text event.</param>
    /// <returns><see langword="true"/> when the key types a character and neither Control nor Alt is held.</returns>
    internal static bool TryReadText(byte* raw, out WindowEvent typed)
    {
        typed = default;
        var modifiers = X11Input.ToModifiers(*(uint*)(raw + InputStateOffset));
        if ((modifiers & (ViewerModifiers.Control | ViewerModifiers.Alt | ViewerModifiers.Meta)) != 0)
        {
            return false;
        }

        nuint symbol;
        byte* unused = stackalloc byte[TextBuffer];
        _ = NativeMethods.XLookupString(raw, unused, TextBuffer, &symbol, null);
        var scalar = X11Input.ToScalar((uint)symbol);
        if (scalar == 0)
        {
            return false;
        }

        typed = new(WindowEventKind.TextInput, *(int*)(raw + InputXOffset), *(int*)(raw + InputYOffset), scalar, (uint)modifiers);
        return true;
    }

    /// <summary>Reads a key event. The key is the one on the key cap without Shift, so Control and a letter is found on any Latin layout.</summary>
    /// <param name="kind">Whether the key went down or came up.</param>
    /// <param name="raw">The XEvent bytes.</param>
    /// <returns>The event.</returns>
    private static WindowEvent ReadKey(WindowEventKind kind, byte* raw)
    {
        var symbol = (uint)NativeMethods.XLookupKeysym(raw, 0);
        var state = *(uint*)(raw + InputStateOffset);
        return new(kind, *(int*)(raw + InputXOffset), *(int*)(raw + InputYOffset), (uint)X11Input.ToKey(symbol), (uint)X11Input.ToModifiers(state));
    }

    /// <summary>Reads a button event. X11 reports the wheel as buttons, which become scroll events.</summary>
    /// <param name="raw">The XEvent bytes.</param>
    /// <param name="pressed">Whether the button went down.</param>
    /// <returns>The event, or the default for a wheel release or an extra button.</returns>
    private static WindowEvent ReadButton(byte* raw, bool pressed)
    {
        var detail = *(uint*)(raw + InputDetailOffset);
        var modifiers = (uint)X11Input.ToModifiers(*(uint*)(raw + InputStateOffset));
        var wheel = X11Input.ToWheel(detail);
        if (wheel != default)
        {
            return pressed ? new(WindowEventKind.Scrolled, wheel.Sideways, wheel.Upward, 0, modifiers) : default;
        }

        var button = (uint)X11Input.ToButton(detail);
        if (button == 0)
        {
            return default;
        }

        return new(pressed ? WindowEventKind.PointerDown : WindowEventKind.PointerUp, *(int*)(raw + InputXOffset), *(int*)(raw + InputYOffset), button, modifiers);
    }

    /// <summary>Reads a pointer move.</summary>
    /// <param name="raw">The XEvent bytes.</param>
    /// <returns>The event.</returns>
    private static WindowEvent ReadMotion(byte* raw)
    {
        var state = *(uint*)(raw + InputStateOffset);
        return new(
            WindowEventKind.PointerMoved,
            *(int*)(raw + InputXOffset),
            *(int*)(raw + InputYOffset),
            (uint)X11Input.ToHeldButtons(state),
            (uint)X11Input.ToModifiers(state));
    }
}
