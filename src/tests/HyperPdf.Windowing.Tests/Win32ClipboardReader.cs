// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HyperPdf.Windowing.Tests;

/// <summary>Reads text from the Windows clipboard the way another application does, so a test can check what a window copied.</summary>
[SupportedOSPlatform("windows")]
internal static class Win32ClipboardReader
{
    /// <summary>The clipboard format of UTF-16 text.</summary>
    private const uint UnicodeText = 13;

    /// <summary>Reads the text on the clipboard.</summary>
    /// <param name="window">A window to open the clipboard for.</param>
    /// <returns>The text, or <see langword="null"/> when the clipboard holds none or cannot be opened.</returns>
    internal static string? ReadText(nint window)
    {
        if (NativeMethods.OpenClipboard(window) == 0)
        {
            return null;
        }

        string? text = null;
        var memory = NativeMethods.GetClipboardData(UnicodeText);
        if (memory != 0)
        {
            var characters = NativeMethods.GlobalLock(memory);
            text = characters == 0 ? null : Marshal.PtrToStringUni(characters);
            _ = NativeMethods.GlobalUnlock(memory);
        }

        _ = NativeMethods.CloseClipboard();
        return text;
    }
}
