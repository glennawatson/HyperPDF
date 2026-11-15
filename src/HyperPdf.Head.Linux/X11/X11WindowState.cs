// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.X11;

/// <summary>Asks the window manager to change a state of a window, such as filling the screen, with the message the desktop standards define.</summary>
internal static unsafe class X11WindowState
{
    /// <summary>The size of an X event in bytes.</summary>
    private const int XEventSize = 192;

    /// <summary>The event type of a client message.</summary>
    private const int ClientMessage = 33;

    /// <summary>The offset of the window in a client message.</summary>
    private const int WindowOffset = 32;

    /// <summary>The offset of the message type in a client message.</summary>
    private const int MessageTypeOffset = 40;

    /// <summary>The offset of the format in a client message.</summary>
    private const int FormatOffset = 48;

    /// <summary>The offset of the first value in a client message.</summary>
    private const int DataOffset = 56;

    /// <summary>The number of bits in each value of the message.</summary>
    private const int ThirtyTwoBits = 32;

    /// <summary>The bytes of each value of the message, which is a C long.</summary>
    private const int ValueSize = 8;

    /// <summary>The wish to be told when a window's structure changes, and to have a say in it. Both bits are what the root window needs to read the message.</summary>
    private const nint StructureMask = (1 << 20) | (1 << 19);

    /// <summary>The action that turns a state off.</summary>
    private const nint Remove = 0;

    /// <summary>The action that turns a state on.</summary>
    private const nint Add = 1;

    /// <summary>The source of the message: an ordinary application.</summary>
    private const nint Application = 1;

    /// <summary>The value after the state atoms, which is the source.</summary>
    private const int SourceIndex = 3;

    /// <summary>The value that holds the state atom.</summary>
    private const int StateIndex = 1;

    /// <summary>Asks the window manager to make a window the active one and bring it to the front.</summary>
    /// <param name="display">The display.</param>
    /// <param name="root">The root window, which the message is sent to.</param>
    /// <param name="window">The window.</param>
    /// <param name="activeAtom">The atom of <c>_NET_ACTIVE_WINDOW</c>.</param>
    internal static void Activate(nint display, nint root, nint window, nint activeAtom)
    {
        byte* message = stackalloc byte[XEventSize];
        new Span<byte>(message, XEventSize).Clear();
        *(int*)message = ClientMessage;
        *(nint*)(message + WindowOffset) = window;
        *(nint*)(message + MessageTypeOffset) = activeAtom;
        *(int*)(message + FormatOffset) = ThirtyTwoBits;
        *(nint*)(message + DataOffset) = Application;
        _ = NativeMethods.XSendEvent(display, root, 0, StructureMask, message);
        _ = NativeMethods.XFlush(display);
    }

    /// <summary>Turns a window state on or off.</summary>
    /// <param name="display">The display.</param>
    /// <param name="root">The root window, which the message is sent to.</param>
    /// <param name="window">The window.</param>
    /// <param name="property">The atom of <c>_NET_WM_STATE</c>.</param>
    /// <param name="state">The atom of the state, such as <c>_NET_WM_STATE_FULLSCREEN</c>.</param>
    /// <param name="on">Whether the state is turned on.</param>
    internal static void Change(nint display, nint root, nint window, nint property, nint state, bool on)
    {
        byte* message = stackalloc byte[XEventSize];
        new Span<byte>(message, XEventSize).Clear();
        *(int*)message = ClientMessage;
        *(nint*)(message + WindowOffset) = window;
        *(nint*)(message + MessageTypeOffset) = property;
        *(int*)(message + FormatOffset) = ThirtyTwoBits;
        *(nint*)(message + DataOffset) = on ? Add : Remove;
        *(nint*)(message + DataOffset + (StateIndex * ValueSize)) = state;
        *(nint*)(message + DataOffset + (SourceIndex * ValueSize)) = Application;
        _ = NativeMethods.XSendEvent(display, root, 0, StructureMask, message);
        _ = NativeMethods.XFlush(display);
    }
}
