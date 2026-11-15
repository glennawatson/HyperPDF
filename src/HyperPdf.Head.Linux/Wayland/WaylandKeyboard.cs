// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using HyperPdf.Presentation.Input;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>
/// The state of the Wayland keyboard: the layout the compositor sent and the modifier keys held. It turns the kernel's key codes into
/// viewer keys and typed characters by the user's layout, and by key position only when no layout could be read.
/// </summary>
[DebuggerDisplay("WaylandKeyboard: group {_group}")]
internal sealed class WaylandKeyboard
{
    /// <summary>The XKB key code of a kernel key code is this much larger.</summary>
    private const uint KeycodeOffset = 8;

    /// <summary>The wl_keyboard key map format for XKB text.</summary>
    private const uint XkbTextFormat = 1;

    /// <summary>The largest key map read. A real map is well under a megabyte.</summary>
    private const uint LargestMap = 4 * 1024 * 1024;

    /// <summary>The Shift bit of an XKB modifier mask.</summary>
    private const uint ShiftMask = 1;

    /// <summary>The Caps Lock bit of an XKB modifier mask.</summary>
    private const uint LockMask = 1 << 1;

    /// <summary>The Num Lock bit of an XKB modifier mask on the standard maps.</summary>
    private const uint NumLockMask = 1 << 4;

    /// <summary>The third level bit of an XKB modifier mask on the standard maps, held by AltGr.</summary>
    private const uint LevelThreeMask = 1 << 7;

    /// <summary>The level of a key with AltGr held.</summary>
    private const int ThirdLevel = 2;

    /// <summary>The first key symbol of the number pad keys that type a character.</summary>
    private const uint FirstPadSymbol = 0xFFAA;

    /// <summary>The last key symbol of the number pad keys that type a character.</summary>
    private const uint LastPadSymbol = 0xFFB9;

    /// <summary>What a number pad key symbol is larger than its character by.</summary>
    private const uint PadSymbolOffset = 0xFF80;

    /// <summary>The first lower case Latin letter key symbol.</summary>
    private const uint FirstLetter = 0x61;

    /// <summary>The last lower case Latin letter key symbol.</summary>
    private const uint LastLetter = 0x7A;

    /// <summary>The layout, or <see langword="null"/> when none could be read.</summary>
    private XkbKeymap? _keymap;

    /// <summary>The modifier keys held or latched, as XKB bits.</summary>
    private uint _held;

    /// <summary>The modifier keys locked, as XKB bits.</summary>
    private uint _locked;

    /// <summary>The layout group in use.</summary>
    private int _group;

    /// <summary>Gets the modifier keys held, as <see cref="ViewerModifiers"/> bits.</summary>
    internal uint Modifiers { get; private set; }

    /// <summary>Reads the layout from the file the compositor sent, and closes the file.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <param name="format">The wl_keyboard key map format.</param>
    /// <param name="size">The bytes in the file.</param>
    internal void LoadKeymap(int descriptor, uint format, uint size)
    {
        using var file = new SafeFileHandle(descriptor, true);
        _keymap = null;
        if (format != XkbTextFormat || size == 0 || size > LargestMap)
        {
            return;
        }

        var text = ArrayPool<byte>.Shared.Rent((int)size);
        try
        {
            // The file's own position is shared with the compositor, so the read names its place.
            var read = 0;
            while (read < size && RandomAccess.Read(file, text.AsSpan(read, (int)size - read), read) is > 0 and var count)
            {
                read += count;
            }

            _keymap = XkbKeymap.Parse(text.AsSpan(0, read));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            // Without the map, or without libX11 to name its symbols, keys are mapped by position.
            _keymap = null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(text);
        }
    }

    /// <summary>Takes the modifier state the compositor reports.</summary>
    /// <param name="depressed">The modifiers held down.</param>
    /// <param name="latched">The modifiers latched for the next key.</param>
    /// <param name="locked">The modifiers locked on.</param>
    /// <param name="group">The layout group in use.</param>
    internal void SetModifiers(uint depressed, uint latched, uint locked, uint group)
    {
        _held = depressed | latched;
        _locked = locked;
        _group = (int)Math.Min(group, XkbKeymap.Groups - 1);

        // The held modifier mask uses the same bits as X11 state on the standard key maps: Shift, Control, Mod1 for Alt and Mod4 for Meta.
        Modifiers = (uint)X11.X11Input.ToModifiers(_held);
    }

    /// <summary>Finds the viewer key of a key.</summary>
    /// <param name="code">The kernel's key code.</param>
    /// <returns>The key.</returns>
    internal ViewerKey ToKey(uint code)
    {
        if (_keymap is not { } keymap)
        {
            return EvdevInput.ToKey(code);
        }

        var symbol = keymap.Symbol(code + KeycodeOffset, _group, 0);
        var key = X11.X11Input.ToKey(symbol);

        // A key that gives a Latin letter is that letter. A key of another script keeps the shortcut of its place on the keyboard,
        // so Control and C still copies on a Cyrillic or Greek layout.
        return key != ViewerKey.Other || symbol is >= FirstLetter and <= LastLetter ? key : EvdevInput.ToKey(code);
    }

    /// <summary>Finds the character a key types with the modifiers held.</summary>
    /// <param name="code">The kernel's key code.</param>
    /// <returns>The Unicode scalar value, or 0 when the key types nothing.</returns>
    internal uint ToScalar(uint code)
    {
        var shift = (_held & ShiftMask) != 0;
        if (_keymap is not { } keymap)
        {
            return EvdevInput.ToScalar(code, shift);
        }

        var keycode = code + KeycodeOffset;
        var third = (_held & LevelThreeMask) != 0 ? ThirdLevel : 0;
        var plain = ToCharacter(keymap.Symbol(keycode, _group, third));
        var shifted = ToCharacter(keymap.Symbol(keycode, _group, third + 1));

        // Num Lock chooses the digit of a number pad key, and Caps Lock the capital of a letter. Shift undoes either.
        var locks = ((_locked & NumLockMask) != 0 && IsPad(keymap.Symbol(keycode, _group, 1)))
            || ((_locked & LockMask) != 0 && Rune.IsValid(plain) && Rune.IsLetter(new(plain)));
        return shift != locks && shifted != 0 ? shifted : plain;
    }

    /// <summary>Tells whether a key symbol is a number pad key that types a character.</summary>
    /// <param name="symbol">The key symbol.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPad(uint symbol) => symbol is >= FirstPadSymbol and <= LastPadSymbol;

    /// <summary>Finds the character a key symbol types.</summary>
    /// <param name="symbol">The key symbol.</param>
    /// <returns>The Unicode scalar value, or 0.</returns>
    private static uint ToCharacter(uint symbol) => IsPad(symbol) ? symbol - PadSymbolOffset : X11.X11Input.ToScalar(symbol);
}
