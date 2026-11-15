// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Compat;

/// <summary>Repeats the bytes of a vector, each one twice in order. .NET 11 interleaves the vector with itself; .NET 10 widens each byte and copies it into the byte above.</summary>
internal static class ByteLanes
{
#if !NET11_0_OR_GREATER
    /// <summary>The bits in a byte, the shift that copies a byte into the byte above it in a 16-bit lane.</summary>
    private const int ByteBits = 8;
#endif

    /// <summary>Repeats each of the lower eight bytes twice.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Bytes 0 to 7, each one twice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> RepeatLower(Vector128<byte> bytes)
    {
#if NET11_0_OR_GREATER
        return Vector128.ZipLower(bytes, bytes);
#else
        var wide = Vector128.WidenLower(bytes);
        return (wide | Vector128.ShiftLeft(wide, ByteBits)).AsByte();
#endif
    }

    /// <summary>Repeats each of the upper eight bytes twice.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Bytes 8 to 15, each one twice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> RepeatUpper(Vector128<byte> bytes)
    {
#if NET11_0_OR_GREATER
        return Vector128.ZipUpper(bytes, bytes);
#else
        var wide = Vector128.WidenUpper(bytes);
        return (wide | Vector128.ShiftLeft(wide, ByteBits)).AsByte();
#endif
    }
}
