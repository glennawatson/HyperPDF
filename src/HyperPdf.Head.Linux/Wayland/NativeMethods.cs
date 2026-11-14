// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Source generated entry points of libc that the Wayland layer needs: an anonymous memory file and sending a file descriptor.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name, resolved through <see cref="Core.Platform.NativeLibraries"/>.</summary>
    internal const string Library = "c";

    /// <summary>Native <c>memfd_create</c>.</summary>
    /// <param name="name">The UTF-8 name, shown in /proc.</param>
    /// <param name="flags">The flags, 0 for none.</param>
    /// <returns>The file descriptor, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "memfd_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int MemfdCreate(string name, uint flags);

    /// <summary>Native <c>sendmsg</c>.</summary>
    /// <param name="socket">The socket descriptor.</param>
    /// <param name="message">The msghdr.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The bytes sent, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "sendmsg")]
    internal static partial nint SendMessage(int socket, byte* message, int flags);
}
