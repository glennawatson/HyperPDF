// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Shows a password as dots in the typing box of a window made of rows, and works the password out again from what is typed over the dots.</summary>
internal static class Passwords
{
    /// <summary>The character shown in place of each character of a password.</summary>
    private const char Dot = '•';

    /// <summary>Hides a password as it is shown in its typing box, one dot for each character. It is never drawn or handed to a screen reader.</summary>
    /// <param name="password">The password.</param>
    /// <returns>The dots.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Mask(string password) => new(Dot, password.Length);

    /// <summary>
    /// Works out a password from what its typing box holds after a key. The box shows dots, so the text that comes back is the dots
    /// with a typed character at the end, or one dot fewer after Backspace.
    /// </summary>
    /// <param name="password">The password before the key.</param>
    /// <param name="shown">The text of the typing box after the key.</param>
    /// <returns>The password after the key.</returns>
    internal static string Typed(string password, string shown)
    {
        if (shown.Length < password.Length)
        {
            return password[..shown.Length];
        }

        return shown.Length > password.Length ? string.Concat(password, shown.AsSpan(password.Length)) : password;
    }
}
