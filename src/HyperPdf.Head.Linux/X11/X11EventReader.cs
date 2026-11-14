// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Linux.X11;

/// <summary>
/// Turns a raw Xlib event into a <see cref="WindowEvent"/>. Offsets are those of the LP64 layout used by 64-bit Linux, read straight
/// from the buffer so no struct holding raw pointers is needed.
/// </summary>
internal static unsafe class X11EventReader
{
    /// <summary>The Xlib event type of a key press.</summary>
    private const int KeyPressType = 2;

    /// <summary>The Xlib event type of a key release.</summary>
    private const int KeyReleaseType = 3;

    /// <summary>The Xlib event type of a button press.</summary>
    private const int ButtonPressType = 4;

    /// <summary>The Xlib event type of a button release.</summary>
    private const int ButtonReleaseType = 5;

    /// <summary>The Xlib event type of pointer motion.</summary>
    private const int MotionNotifyType = 6;

    /// <summary>The Xlib event type of an exposure.</summary>
    private const int ExposeType = 12;

    /// <summary>The Xlib event type of a configure notify.</summary>
    private const int ConfigureNotifyType = 22;

    /// <summary>The Xlib event type of a client message.</summary>
    private const int ClientMessageType = 33;

    /// <summary>The offset of the width in a configure event.</summary>
    private const int ConfigureWidthOffset = 56;

    /// <summary>The offset of the height in a configure event.</summary>
    private const int ConfigureHeightOffset = 60;

    /// <summary>The offset of the first data word in a client message.</summary>
    private const int MessageDataOffset = 56;

    /// <summary>The offset of the pointer x in a key, button or motion event.</summary>
    private const int InputXOffset = 64;

    /// <summary>The offset of the pointer y in a key, button or motion event.</summary>
    private const int InputYOffset = 68;

    /// <summary>The offset of the modifier bits in a key, button or motion event.</summary>
    private const int InputStateOffset = 80;

    /// <summary>The offset of the key code or button number in a key, button or motion event.</summary>
    private const int InputDetailOffset = 84;

    /// <summary>Reads one event.</summary>
    /// <param name="raw">The raw XEvent.</param>
    /// <param name="deleteAtom">The close request atom.</param>
    /// <param name="windowEvent">Receives the event.</param>
    /// <returns><see langword="true"/> when the event is one the viewer uses.</returns>
    internal static bool TryRead(byte* raw, nint deleteAtom, out WindowEvent windowEvent)
    {
        var type = *(int*)raw;
        windowEvent = type switch
        {
            ClientMessageType when *(nint*)(raw + MessageDataOffset) == deleteAtom => new(WindowEventKind.CloseRequested, 0, 0, 0, 0),
            ConfigureNotifyType => new(WindowEventKind.Resized, *(int*)(raw + ConfigureWidthOffset), *(int*)(raw + ConfigureHeightOffset), 0, 0),
            ExposeType => new(WindowEventKind.Exposed, 0, 0, 0, 0),
            KeyPressType or KeyReleaseType or ButtonPressType or ButtonReleaseType or MotionNotifyType => ReadInput(type, raw),
            _ => default,
        };

        return windowEvent != default;
    }

    /// <summary>Reads a key, button or motion event.</summary>
    /// <param name="type">The Xlib type.</param>
    /// <param name="raw">The raw XEvent.</param>
    /// <returns>The event.</returns>
    private static WindowEvent ReadInput(int type, byte* raw)
    {
        var kind = type switch
        {
            KeyPressType => WindowEventKind.KeyDown,
            KeyReleaseType => WindowEventKind.KeyUp,
            ButtonPressType => WindowEventKind.PointerDown,
            ButtonReleaseType => WindowEventKind.PointerUp,
            _ => WindowEventKind.PointerMoved,
        };
        return new(kind, *(int*)(raw + InputXOffset), *(int*)(raw + InputYOffset), *(uint*)(raw + InputDetailOffset), *(uint*)(raw + InputStateOffset));
    }
}
