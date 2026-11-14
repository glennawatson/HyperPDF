// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Source generated entry points of libc that the Linux head needs: an anonymous memory file, sending a file descriptor, and waiting on descriptors with a wake-up.</summary>
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

    /// <summary>Native <c>recvmsg</c>.</summary>
    /// <param name="socket">The socket descriptor.</param>
    /// <param name="message">The msghdr, which receives the data and any file descriptors sent with it.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The bytes received, 0 when the peer closed, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "recvmsg")]
    internal static partial nint ReceiveMessage(int socket, byte* message, int flags);

    /// <summary>Native <c>pipe2</c>.</summary>
    /// <param name="descriptors">Receives the read end and then the write end.</param>
    /// <param name="flags">The flags for both ends.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "pipe2")]
    internal static partial int Pipe(int* descriptors, int flags);

    /// <summary>Native <c>eventfd</c>.</summary>
    /// <param name="initial">The starting counter.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The file descriptor, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "eventfd")]
    internal static partial int EventDescriptor(uint initial, int flags);

    /// <summary>Native <c>poll</c>.</summary>
    /// <param name="descriptors">The pollfd array.</param>
    /// <param name="count">The number of entries.</param>
    /// <param name="timeoutMilliseconds">The longest wait, or -1 for no limit.</param>
    /// <returns>The number of ready descriptors, 0 on a timeout, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "poll")]
    internal static partial int Poll(byte* descriptors, nuint count, int timeoutMilliseconds);

    /// <summary>Native <c>read</c>.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <param name="buffer">Receives the bytes.</param>
    /// <param name="count">The most bytes to read.</param>
    /// <returns>The bytes read, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "read")]
    internal static partial nint Read(int descriptor, byte* buffer, nuint count);

    /// <summary>Native <c>write</c>.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <param name="buffer">The bytes.</param>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes written, or -1 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "write")]
    internal static partial nint Write(int descriptor, byte* buffer, nuint count);

    /// <summary>Native <c>close</c>.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <returns>0 on success.</returns>
    [LibraryImport(Library, EntryPoint = "close")]
    internal static partial int Close(int descriptor);
}
