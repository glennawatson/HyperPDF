// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Head.Linux.X11;

/// <summary>
/// Keeps the program running when the X server reports an error for a request that nobody waits for, such as a reply to a drag whose
/// source window went away. Without a handler the X library ends the whole process for any such error.
/// </summary>
internal static unsafe class X11ErrorHandler
{
    /// <summary>Whether the handler was installed.</summary>
    private static int _installed;

    /// <summary>Installs the handler, once for the process.</summary>
    internal static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 0)
        {
            _ = NativeMethods.XSetErrorHandler(&Ignore);
        }
    }

    /// <summary>Takes an error and carries on. The window a request named is gone, which is what the sender of the request finds out for itself.</summary>
    /// <param name="display">The display.</param>
    /// <param name="error">The error event.</param>
    /// <returns>Always 0.</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Ignore(nint display, byte* error)
    {
        _ = display;
        _ = error;
        return 0;
    }
}
