// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Puts text on the Windows clipboard.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe class Win32Clipboard
{
    /// <summary>The clipboard format of UTF-16 text.</summary>
    private const uint UnicodeText = 13;

    /// <summary>The allocation flag of memory the system may move, which the clipboard requires.</summary>
    private const uint Movable = 2;

    /// <summary>Replaces what is on the clipboard with text.</summary>
    /// <param name="window">The window that sets the clipboard.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the clipboard took the text.</returns>
    internal static bool SetText(nint window, string text)
    {
        // The clipboard wants the text with its terminator, in memory it can take over.
        var bytes = (nuint)((text.Length + 1) * sizeof(char));
        var memory = NativeMethods.GlobalAlloc(Movable, bytes);
        if (memory == 0)
        {
            return false;
        }

        var target = NativeMethods.GlobalLock(memory);
        if (target is null)
        {
            _ = NativeMethods.GlobalFree(memory);
            return false;
        }

        var characters = new Span<char>(target, text.Length + 1);
        text.CopyTo(characters);
        characters[text.Length] = '\0';
        _ = NativeMethods.GlobalUnlock(memory);
        if (NativeMethods.OpenClipboard(window) == 0)
        {
            // Another application holds the clipboard right now.
            _ = NativeMethods.GlobalFree(memory);
            return false;
        }

        _ = NativeMethods.EmptyClipboard();
        var taken = NativeMethods.SetClipboardData(UnicodeText, memory) != 0;
        _ = NativeMethods.CloseClipboard();
        if (!taken)
        {
            // The system only owns the memory once it has taken the data.
            _ = NativeMethods.GlobalFree(memory);
        }

        return taken;
    }
}
