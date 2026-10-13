// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The sfnt table checksum (OpenType, "Table directory": the sum of the table's big-endian 32-bit words, modulo 2^32, with
/// the last word padded with zeros). The sum does not depend on the order of the words, so vector lanes add the swapped
/// words in parallel and the scalar loop gives the same result.
/// </summary>
internal static class SfntChecksum
{
    /// <summary>The bytes in a checksum word.</summary>
    private const int WordBytes = 4;

    /// <summary>Gets the shuffle that reverses the bytes of each 32-bit lane: the bytes 3, 2, 1, 0, 7, 6, 5, 4 and 11, 10, 9, 8, 15, 14, 13, 12.</summary>
    private static Vector128<byte> ReverseWords => Vector128.Create(0x0405060700010203UL, 0x0C0D0E0F08090A0BUL).AsByte();

    /// <summary>Adds up big-endian 32-bit words, the last one padded with zeros.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The checksum.</returns>
    internal static uint Compute(ReadOnlySpan<byte> data)
    {
        var whole = data.Length & -WordBytes;
        var position = 0;
        uint sum = 0;
        if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian && whole >= Vector128<byte>.Count)
        {
            ref var start = ref MemoryMarshal.GetReference(data);
            var reverse = ReverseWords;
            var lanes = Vector128<uint>.Zero;
            for (; position <= whole - Vector128<byte>.Count; position += Vector128<byte>.Count)
            {
                lanes += Vector128.Shuffle(Vector128.LoadUnsafe(ref start, (nuint)position), reverse).AsUInt32();
            }

            sum = Vector128.Sum(lanes);
        }

        for (; position < whole; position += WordBytes)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
        }

        if (whole < data.Length)
        {
            Span<byte> last = stackalloc byte[WordBytes];
            last.Clear();
            data[whole..].CopyTo(last);
            sum += BinaryPrimitives.ReadUInt32BigEndian(last);
        }

        return sum;
    }
}
