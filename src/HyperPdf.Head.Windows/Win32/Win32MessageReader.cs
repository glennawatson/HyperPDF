// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Turns a Win32 window message into a <see cref="WindowEvent"/>.</summary>
internal static class Win32MessageReader
{
    /// <summary>The WM_CLOSE message.</summary>
    internal const uint Close = 0x0010;

    /// <summary>The WM_SIZE message.</summary>
    internal const uint Size = 0x0005;

    /// <summary>The WM_PAINT message.</summary>
    internal const uint Paint = 0x000F;

    /// <summary>The WM_KEYDOWN message.</summary>
    private const uint KeyDown = 0x0100;

    /// <summary>The WM_KEYUP message.</summary>
    private const uint KeyUp = 0x0101;

    /// <summary>The WM_SYSKEYDOWN message, sent for keys pressed with Alt.</summary>
    private const uint SysKeyDown = 0x0104;

    /// <summary>The WM_SYSKEYUP message.</summary>
    private const uint SysKeyUp = 0x0105;

    /// <summary>The WM_MOUSEMOVE message.</summary>
    private const uint MouseMove = 0x0200;

    /// <summary>The WM_LBUTTONDOWN message.</summary>
    private const uint LeftDown = 0x0201;

    /// <summary>The WM_LBUTTONUP message.</summary>
    private const uint LeftUp = 0x0202;

    /// <summary>The WM_RBUTTONDOWN message.</summary>
    private const uint RightDown = 0x0204;

    /// <summary>The WM_RBUTTONUP message.</summary>
    private const uint RightUp = 0x0205;

    /// <summary>The WM_MBUTTONDOWN message.</summary>
    private const uint MiddleDown = 0x0207;

    /// <summary>The WM_MBUTTONUP message.</summary>
    private const uint MiddleUp = 0x0208;

    /// <summary>The WM_MOUSEWHEEL message.</summary>
    private const uint MouseWheel = 0x020A;

    /// <summary>The WM_MOUSEHWHEEL message.</summary>
    private const uint MouseHorizontalWheel = 0x020E;

    /// <summary>The button reported for the left button.</summary>
    private const uint LeftButton = (uint)ViewerButtons.Left;

    /// <summary>The button reported for the middle button.</summary>
    private const uint MiddleButton = (uint)ViewerButtons.Middle;

    /// <summary>The button reported for the right button.</summary>
    private const uint RightButton = (uint)ViewerButtons.Right;

    /// <summary>The primary value of WM_SIZE when the window is minimised.</summary>
    private const nuint SizeMinimized = 1;

    /// <summary>The bits in a 16 bit half word.</summary>
    private const int HalfWord = 16;

    /// <summary>The mask of a 16 bit half word.</summary>
    private const int HalfMask = 0xFFFF;

    /// <summary>Reads one message.</summary>
    /// <param name="message">The message number.</param>
    /// <param name="primary">The first parameter.</param>
    /// <param name="secondary">The second parameter.</param>
    /// <param name="modifiers">The modifier bits at the time of the message.</param>
    /// <returns>The event, or a default event when the viewer does not use the message.</returns>
    internal static WindowEvent Read(uint message, nuint primary, nint secondary, uint modifiers) => message switch
    {
        Close => new(WindowEventKind.CloseRequested, 0, 0, 0, 0),
        Size when primary != SizeMinimized => new(WindowEventKind.Resized, LowWord(secondary), HighWord(secondary), 0, 0),
        Paint => new(WindowEventKind.Exposed, 0, 0, 0, 0),
        KeyDown or SysKeyDown => new(WindowEventKind.KeyDown, 0, 0, (uint)Win32Input.ToKey((uint)primary), modifiers),
        KeyUp or SysKeyUp => new(WindowEventKind.KeyUp, 0, 0, (uint)Win32Input.ToKey((uint)primary), modifiers),
        MouseMove => new(WindowEventKind.PointerMoved, SignedLowWord(secondary), SignedHighWord(secondary), (uint)Win32Input.ToHeldButtons(primary), modifiers),
        LeftDown => Pointer(WindowEventKind.PointerDown, secondary, LeftButton, modifiers),
        LeftUp => Pointer(WindowEventKind.PointerUp, secondary, LeftButton, modifiers),
        RightDown => Pointer(WindowEventKind.PointerDown, secondary, RightButton, modifiers),
        RightUp => Pointer(WindowEventKind.PointerUp, secondary, RightButton, modifiers),
        MiddleDown => Pointer(WindowEventKind.PointerDown, secondary, MiddleButton, modifiers),
        MiddleUp => Pointer(WindowEventKind.PointerUp, secondary, MiddleButton, modifiers),
        MouseWheel => new(WindowEventKind.Scrolled, 0, WheelDelta(primary), 0, modifiers),
        MouseHorizontalWheel => new(WindowEventKind.Scrolled, SidewaysWheelDelta(primary), 0, 0, modifiers),
        _ => default,
    };

    /// <summary>Builds a button event.</summary>
    /// <param name="kind">Down or up.</param>
    /// <param name="secondary">The position parameter.</param>
    /// <param name="button">The button number.</param>
    /// <param name="modifiers">The modifier bits.</param>
    /// <returns>The event.</returns>
    private static WindowEvent Pointer(WindowEventKind kind, nint secondary, uint button, uint modifiers) =>
        new(kind, SignedLowWord(secondary), SignedHighWord(secondary), button, modifiers);

    /// <summary>Gets the low 16 bits, unsigned.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The low word.</returns>
    private static int LowWord(nint value) => (int)(value & HalfMask);

    /// <summary>Gets the high 16 bits of the low 32 bits, unsigned.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The high word.</returns>
    private static int HighWord(nint value) => (int)((value >> HalfWord) & HalfMask);

    /// <summary>Gets the low 16 bits, signed, as mouse coordinates are on a multi monitor desktop.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The low word.</returns>
    private static int SignedLowWord(nint value) => (short)(value & HalfMask);

    /// <summary>Gets the high 16 bits of the low 32 bits, signed.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The high word.</returns>
    private static int SignedHighWord(nint value) => (short)((value >> HalfWord) & HalfMask);

    /// <summary>Gets the wheel amount, which is in the high word of primary and signed.</summary>
    /// <param name="primary">The first parameter.</param>
    /// <returns>The wheel amount.</returns>
    private static int WheelDelta(nuint primary) => (short)((primary >> HalfWord) & HalfMask);

    /// <summary>Gets the sideways wheel amount. Windows counts a tilt to the right as positive, and the viewer counts left as positive.</summary>
    /// <param name="primary">The first parameter.</param>
    /// <returns>The wheel amount.</returns>
    private static int SidewaysWheelDelta(nuint primary) => -WheelDelta(primary);
}
