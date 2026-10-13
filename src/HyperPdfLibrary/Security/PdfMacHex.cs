// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>
/// The strict hexadecimal form of a PDF MAC token (ISO/TS 32004:2024 Table 6): digits only, an even count, no white space,
/// and no dropped final digit, which the general string syntax of ISO 32000-2 7.3.4.3 would allow.
/// </summary>
internal static class PdfMacHex
{
    /// <summary>The hexadecimal digits per byte.</summary>
    internal const int DigitsPerByte = 2;

    /// <summary>The bits in a hexadecimal digit.</summary>
    private const int NibbleBits = 4;

    /// <summary>The value of the letter A as a digit.</summary>
    private const int LetterOffset = 10;

    /// <summary>The mask of the low nibble.</summary>
    private const int NibbleMask = 0xF;

    /// <summary>Gets the upper-case digits.</summary>
    private static ReadOnlySpan<byte> Digits => "0123456789ABCDEF"u8;

    /// <summary>Decodes digits into bytes.</summary>
    /// <param name="digits">The ASCII digits.</param>
    /// <param name="destination">Receives the bytes; it must be half as long as the digits.</param>
    /// <returns><see langword="true"/> when every character is a hexadecimal digit and the count is even.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> digits, Span<byte> destination)
    {
        if (digits.Length != destination.Length * DigitsPerByte)
        {
            return false;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            var high = Nibble(digits[i * DigitsPerByte]);
            var low = Nibble(digits[(i * DigitsPerByte) + 1]);
            if ((high | low) < 0)
            {
                return false;
            }

            destination[i] = (byte)((high << NibbleBits) | low);
        }

        return true;
    }

    /// <summary>Encodes bytes as upper-case digits.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="destination">Receives the digits; it must be twice as long as the bytes.</param>
    internal static void Encode(ReadOnlySpan<byte> bytes, Span<byte> destination)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            destination[i * DigitsPerByte] = Digits[bytes[i] >> NibbleBits];
            destination[(i * DigitsPerByte) + 1] = Digits[bytes[i] & NibbleMask];
        }
    }

    /// <summary>Gets the value of a digit.</summary>
    /// <param name="digit">The ASCII character.</param>
    /// <returns>The value, or -1 when the character is not a hexadecimal digit.</returns>
    private static int Nibble(byte digit) => digit switch
    {
        >= (byte)'0' and <= (byte)'9' => digit - (byte)'0',
        >= (byte)'A' and <= (byte)'F' => digit - (byte)'A' + LetterOffset,
        >= (byte)'a' and <= (byte)'f' => digit - (byte)'a' + LetterOffset,
        _ => -1,
    };
}
