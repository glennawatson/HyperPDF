// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Compat;

/// <summary>Opens read-only streams over bytes held in memory, using .NET 11's allocation-light type where it exists.</summary>
internal static class MemoryStreams
{
    /// <summary>Opens a read-only stream over bytes.</summary>
    /// <param name="bytes">The bytes; they must not change while the stream is open.</param>
    /// <returns>The stream; the caller disposes it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Stream OpenRead(ReadOnlyMemory<byte> bytes) =>
#if NET11_0_OR_GREATER
        new ReadOnlyMemoryStream(bytes);
#else
        MemoryMarshal.TryGetArray(bytes, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false, publiclyVisible: false)
            : new MemoryStream(bytes.ToArray(), false);
#endif
}
