// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes thirteen digits as EAN-13: a guard, six digits, a centre guard, six digits and a guard.</summary>
internal static class Ean13Encoder
{
    /// <summary>The digits before the check digit.</summary>
    private const int DataDigits = 12;

    /// <summary>The digits including the check digit.</summary>
    private const int AllDigits = 13;

    /// <summary>The digit coded by the six left-hand symbols' choice of pattern, so only digits 1 to 6 follow it.</summary>
    private const int LeadDigitCount = 6;

    /// <summary>The weight of every second digit in the check sum.</summary>
    private const int Weight = 3;

    /// <summary>The number the check digit is taken from.</summary>
    private const int Ten = 10;

    /// <summary>The module pattern of each digit in the left-hand set A (odd parity).</summary>
    private static readonly string[] SetA = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];

    /// <summary>The module pattern of each digit in the left-hand set B (even parity).</summary>
    private static readonly string[] SetB = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];

    /// <summary>The module pattern of each digit on the right-hand side.</summary>
    private static readonly string[] SetC = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];

    /// <summary>For each first digit, which of the sets A and B the six left-hand symbols use.</summary>
    private static readonly string[] Parity = ["AAAAAA", "AABABB", "AABBAB", "AABBBA", "ABAABB", "ABBAAB", "ABBBAA", "ABABAB", "ABABBA", "ABBABA"];

    /// <summary>Encodes digits.</summary>
    /// <param name="digits">Twelve digits, whose check digit is added, or thirteen digits whose check digit is verified.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is not twelve or thirteen digits, or the thirteenth digit is not the check digit.</exception>
    internal static PdfBarcode Encode(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        if (digits.Length is not (DataDigits or AllDigits) || !IsAllDigits(digits))
        {
            throw new ArgumentException("An EAN-13 code is twelve or thirteen digits.", nameof(digits));
        }

        var check = (char)('0' + GetCheckDigit(digits));
        if (digits.Length == AllDigits && digits[DataDigits] != check)
        {
            throw new ArgumentException("The thirteenth digit is not the check digit.", nameof(digits));
        }

        var text = digits[..DataDigits] + check;
        LinearModules modules = default;
        modules.AddBits("101");
        var pattern = Parity[text[0] - '0'];
        for (var i = 0; i < LeadDigitCount; i++)
        {
            var digit = text[i + 1] - '0';
            modules.AddBits(pattern[i] == 'A' ? SetA[digit] : SetB[digit]);
        }

        modules.AddBits("01010");
        for (var i = LeadDigitCount + 1; i < AllDigits; i++)
        {
            modules.AddBits(SetC[text[i] - '0']);
        }

        modules.AddBits("101");
        return modules.ToBarcode(PdfBarcodeSymbology.Ean13, text);
    }

    /// <summary>Determines whether every character is an ASCII digit.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when all of it is digits.</returns>
    private static bool IsAllDigits(string text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Works out the check digit of the first twelve digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The check digit.</returns>
    private static int GetCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < DataDigits; i++)
        {
            sum += (digits[i] - '0') * ((i & 1) == 0 ? 1 : Weight);
        }

        return (Ten - (sum % Ten)) % Ten;
    }
}
