// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>Reads the text a key press types from its NSEvent.</summary>
internal static unsafe class CocoaText
{
    /// <summary>The first character that prints.</summary>
    private const int FirstPrinting = 0x20;

    /// <summary>The character the delete key reports.</summary>
    private const int DeleteCharacter = 0x7F;

    /// <summary>The first of the private characters AppKit reports for arrow and function keys.</summary>
    private const int FirstFunctionKey = 0xF700;

    /// <summary>The last of the private characters AppKit reports for arrow and function keys.</summary>
    private const int LastFunctionKey = 0xF8FF;

    /// <summary>Reports the characters a key press types.</summary>
    /// <param name="keyEvent">The NSEvent of a key press.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <param name="handler">Receives one text event for each character.</param>
    internal static void Report(nint keyEvent, uint modifiers, WindowEventHandler handler)
    {
        var characters = NativeMethods.Send(keyEvent, Selectors.Characters);
        var text = characters == 0 ? null : (byte*)NativeMethods.Send(characters, Selectors.Utf8String);
        if (text is null)
        {
            return;
        }

        var bytes = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text);
        while (!bytes.IsEmpty && Rune.DecodeFromUtf8(bytes, out var rune, out var used) == OperationStatus.Done)
        {
            bytes = bytes[used..];
            if (IsText(rune.Value))
            {
                handler(new(WindowEventKind.TextInput, 0, 0, (uint)rune.Value, modifiers));
            }
        }
    }

    /// <summary>Tells whether a character a key reports is text. Arrow and function keys report characters from a private range, which are not.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> for a character that can be typed into a box.</returns>
    private static bool IsText(int scalar) => scalar >= FirstPrinting && scalar != DeleteCharacter && scalar is < FirstFunctionKey or > LastFunctionKey;
}
