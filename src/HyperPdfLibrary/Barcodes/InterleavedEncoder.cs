// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes digits as Interleaved 2 of 5: two digits at a time, the first as bars and the second as the spaces between them.</summary>
internal static class InterleavedEncoder
{
    /// <summary>The width of a wide bar or space, in modules.</summary>
    private const int Wide = 3;

    /// <summary>The digits in one pair.</summary>
    private const int Pair = 2;

    /// <summary>The pattern of each digit: <c>n</c> for a narrow element and <c>w</c> for a wide one.</summary>
    private static readonly string[] Patterns = ["nnwwn", "wnnnw", "nwnnw", "wwnnn", "nnwnw", "wnwnn", "nwwnn", "nnnww", "wnnwn", "nwnwn"];

    /// <summary>Encodes digits.</summary>
    /// <param name="digits">The digits; an odd count gets a zero in front.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character that is not a digit.</exception>
    internal static PdfBarcode Encode(string digits)
    {
        ArgumentException.ThrowIfNullOrEmpty(digits);
        foreach (var digit in digits)
        {
            if (!char.IsAsciiDigit(digit))
            {
                throw new ArgumentException("Interleaved 2 of 5 holds only digits.", nameof(digits));
            }
        }

        var text = (digits.Length & 1) == 1 ? $"0{digits}" : digits;
        LinearModules modules = default;
        modules.AddWidths("1111");
        for (var i = 0; i < text.Length; i += Pair)
        {
            AddPair(ref modules, Patterns[text[i] - '0'], Patterns[text[i + 1] - '0']);
        }

        modules.AddRun(Wide);
        modules.AddRun(1);
        modules.AddRun(1);
        return modules.ToBarcode(PdfBarcodeSymbology.Interleaved2Of5, text);
    }

    /// <summary>Adds a pair of digits: the bars of the first and the spaces of the second, taken in turn.</summary>
    /// <param name="modules">The modules so far.</param>
    /// <param name="bars">The first digit's pattern.</param>
    /// <param name="spaces">The second digit's pattern.</param>
    private static void AddPair(ref LinearModules modules, string bars, string spaces)
    {
        for (var i = 0; i < bars.Length; i++)
        {
            modules.AddRun(bars[i] == 'w' ? Wide : 1);
            modules.AddRun(spaces[i] == 'w' ? Wide : 1);
        }
    }
}
