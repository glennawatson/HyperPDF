// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Converts between bytes and strings of 0 and 1 characters, so fax data can be written as the bit strings the ITU-T recommendations print.</summary>
internal static class CcittBitStrings
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The number base of a bit string.</summary>
    private const int BinaryRadix = 2;

    /// <summary>Turns a bit string into bytes, padding the last byte with zeros.</summary>
    /// <param name="bits">The bits, most significant first.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] ToBytes(string bits)
    {
        var bytes = new byte[(bits.Length + ByteBits - 1) / ByteBits];
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] == '1')
            {
                bytes[i / ByteBits] |= (byte)(1 << (ByteBits - 1 - (i % ByteBits)));
            }
        }

        return bytes;
    }

    /// <summary>Turns bytes into a bit string.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The bits, most significant first.</returns>
    internal static string ToBits(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * ByteBits);
        foreach (var value in bytes)
        {
            _ = text.Append(Convert.ToString(value, BinaryRadix).PadLeft(ByteBits, '0'));
        }

        return text.ToString();
    }

    /// <summary>Pads a bit string with zeros to a whole number of bytes.</summary>
    /// <param name="bits">The bits.</param>
    /// <returns>The padded bits.</returns>
    internal static string Pad(string bits) => bits.PadRight((bits.Length + ByteBits - 1) / ByteBits * ByteBits, '0');
}
