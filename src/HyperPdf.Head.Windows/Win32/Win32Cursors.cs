// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Constants and small helpers for Win32 windows: cursors, struct offsets, modifier keys and the frame bitmap header.</summary>
internal static unsafe class Win32Cursors
{
    /// <summary>The first window width in pixels.</summary>
    internal const int InitialWidth = 1024;

    /// <summary>The first window height in pixels.</summary>
    internal const int InitialHeight = 768;

    /// <summary>The system arrow cursor id.</summary>
    internal const nint Arrow = 32_512;

    /// <summary>The offset of the window procedure in a WNDCLASSEXW.</summary>
    internal const int ProcedureOffset = 8;

    /// <summary>The offset of the module handle in a WNDCLASSEXW.</summary>
    internal const int InstanceOffset = 24;

    /// <summary>The offset of the class name in a WNDCLASSEXW.</summary>
    internal const int ClassNameOffset = 64;

    /// <summary>The mask of the hit test code in the lParam of WM_SETCURSOR.</summary>
    internal const nint HitTestMask = 0xFFFF;

    /// <summary>The hit test code of the client area, HTCLIENT.</summary>
    internal const nint ClientHit = 1;

    /// <summary>The system I-beam cursor id.</summary>
    private const nint IBeam = 32_513;

    /// <summary>The system cross cursor id.</summary>
    private const nint Cross = 32_515;

    /// <summary>The system four arrow cursor id.</summary>
    private const nint SizeAll = 32_646;

    /// <summary>The system hand cursor id.</summary>
    private const nint Hand = 32_649;

    /// <summary>The virtual key code of Shift.</summary>
    private const int ShiftKey = 0x10;

    /// <summary>The virtual key code of Control.</summary>
    private const int ControlKey = 0x11;

    /// <summary>The virtual key code of Alt.</summary>
    private const int AltKey = 0x12;

    /// <summary>The bit that says a key is down in the result of GetKeyState.</summary>
    private const int DownBit = 0x8000;

    /// <summary>The modifier bit reported for Shift.</summary>
    private const uint ShiftBit = (uint)ViewerModifiers.Shift;

    /// <summary>The modifier bit reported for Control.</summary>
    private const uint ControlBit = (uint)ViewerModifiers.Control;

    /// <summary>The modifier bit reported for Alt.</summary>
    private const uint AltBit = (uint)ViewerModifiers.Alt;

    /// <summary>The offset of the width in a BITMAPINFOHEADER.</summary>
    private const int WidthOffset = 4;

    /// <summary>The offset of the height in a BITMAPINFOHEADER.</summary>
    private const int HeightOffset = 8;

    /// <summary>The offset of the plane count in a BITMAPINFOHEADER.</summary>
    private const int PlanesOffset = 12;

    /// <summary>The offset of the bit count in a BITMAPINFOHEADER.</summary>
    private const int BitCountOffset = 14;

    /// <summary>The bits per pixel of the frame.</summary>
    private const short BitsPerPixel = 32;

    /// <summary>The size of a BITMAPINFOHEADER in bytes.</summary>
    private const int HeaderSize = 40;

    /// <summary>Gets the system cursor id for a viewer cursor.</summary>
    /// <param name="cursor">The viewer cursor.</param>
    /// <returns>The system cursor id.</returns>
    internal static nint For(ViewerCursor cursor) => cursor switch
    {
        ViewerCursor.Hand => Hand,
        ViewerCursor.Cross => Cross,
        ViewerCursor.Text => IBeam,
        ViewerCursor.Move => SizeAll,
        _ => Arrow,
    };

    /// <summary>Reads which modifier keys are down now.</summary>
    /// <returns>The modifier bits.</returns>
    internal static uint Modifiers()
    {
        var bits = 0U;
        bits |= IsDown(ShiftKey) ? ShiftBit : 0;
        bits |= IsDown(ControlKey) ? ControlBit : 0;
        bits |= IsDown(AltKey) ? AltBit : 0;
        return bits;
    }

    /// <summary>Writes a top down 32 bit BITMAPINFOHEADER.</summary>
    /// <param name="header">Receives the header, 40 bytes.</param>
    /// <param name="size">The frame size.</param>
    internal static void WriteBitmapHeader(byte* header, SurfaceSize size)
    {
        new Span<byte>(header, HeaderSize).Clear();
        *(int*)header = HeaderSize;
        *(int*)(header + WidthOffset) = size.Width;

        // A negative height tells GDI the first line in memory is the top line.
        *(int*)(header + HeightOffset) = -size.Height;
        *(short*)(header + PlanesOffset) = 1;
        *(short*)(header + BitCountOffset) = BitsPerPixel;
    }

    /// <summary>Tells whether a virtual key is down.</summary>
    /// <param name="key">The virtual key code.</param>
    /// <returns><see langword="true"/> when it is down.</returns>
    private static bool IsDown(int key) => (NativeMethods.GetKeyState(key) & DownBit) != 0;
}
