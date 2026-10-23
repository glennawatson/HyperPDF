// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes text as Code 128, switching between its three code sets to keep the symbol short.</summary>
internal static class Code128Encoder
{
    /// <summary>The value of the start character for code set A.</summary>
    private const int StartA = 103;

    /// <summary>The value of the start character for code set B.</summary>
    private const int StartB = 104;

    /// <summary>The value of the start character for code set C.</summary>
    private const int StartC = 105;

    /// <summary>The value of the stop character.</summary>
    private const int Stop = 106;

    /// <summary>The value that switches to code set C.</summary>
    private const int SwitchToC = 99;

    /// <summary>The value that switches to code set B.</summary>
    private const int SwitchToB = 100;

    /// <summary>The value that switches to code set A.</summary>
    private const int SwitchToA = 101;

    /// <summary>The number the check character is taken from.</summary>
    private const int Modulus = 103;

    /// <summary>The longest run of digits kept in the current set instead of switching to code set C.</summary>
    private const int ShortestDigitRun = 4;

    /// <summary>The highest character Code 128 holds.</summary>
    private const int HighestCharacter = 127;

    /// <summary>The first printable character, whose value in code sets A and B is zero.</summary>
    private const int FirstPrintable = 32;

    /// <summary>The first character that only code set B holds.</summary>
    private const int FirstOnlyInB = 96;

    /// <summary>The offset from a control character to its value in code set A.</summary>
    private const int ControlOffset = 64;

    /// <summary>The digits that make one value in code set C.</summary>
    private const int DigitPair = 2;

    /// <summary>The ten in a pair of digits.</summary>
    private const int Ten = 10;

    /// <summary>The widths of the bars and spaces of each value from 0 to 106: six runs, or seven for the stop character.</summary>
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
    ];

    /// <summary>Encodes text.</summary>
    /// <param name="text">The text: ASCII characters 0 to 127.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character above 127.</exception>
    internal static PdfBarcode Encode(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        foreach (var character in text)
        {
            if (character > HighestCharacter)
            {
                throw new ArgumentException("Code 128 holds only ASCII characters.", nameof(text));
            }
        }

        var values = Code128Values.Create(text.Length);
        try
        {
            FillValues(ref values, text);
            var check = values.Span[0];
            for (var i = 1; i < values.Count; i++)
            {
                check += i * values.Span[i];
            }

            values.Add(check % Modulus);
            values.Add(Stop);
            LinearModules modules = default;
            foreach (var value in values.Span)
            {
                modules.AddWidths(Patterns[value]);
            }

            return modules.ToBarcode(PdfBarcodeSymbology.Code128, text);
        }
        finally
        {
            values.Release();
        }
    }

    /// <summary>Turns text into the values of the start character and the data characters, choosing code sets.</summary>
    /// <param name="values">Receives the start character's value, then the data values.</param>
    /// <param name="text">The text.</param>
    private static void FillValues(ref Code128Values values, string text)
    {
        var set = ' ';
        var position = 0;
        while (position < text.Length)
        {
            var digits = CountDigits(text, position);
            if (digits >= ShortestDigitRun || (set == 'C' && digits >= DigitPair))
            {
                position = AddDigitPairs(ref values, text, position, digits, ref set);
                continue;
            }

            AddCharacter(ref values, text[position], ref set);
            position++;
        }
    }

    /// <summary>Adds one character, switching to code set A for a control character or to B for a lower case letter.</summary>
    /// <param name="values">The values so far.</param>
    /// <param name="character">The character.</param>
    /// <param name="set">The current code set, or a space before the first.</param>
    private static void AddCharacter(ref Code128Values values, char character, ref char set)
    {
        var wanted = character < FirstPrintable ? 'A' : 'B';
        var staysInA = set == 'A' && character >= FirstPrintable && IsInBothSets(character);
        if (set != wanted && !staysInA)
        {
            Switch(ref values, ref set, wanted);
        }

        values.Add(set == 'A' ? GetValueInA(character) : character - FirstPrintable);
    }

    /// <summary>Counts the digits that start at a position.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">Where to start.</param>
    /// <returns>The number of digits in a row.</returns>
    private static int CountDigits(string text, int position)
    {
        var end = position;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return end - position;
    }

    /// <summary>Adds a run of digits in code set C, two to a value, with a leftover digit in the set that came before.</summary>
    /// <param name="values">The values so far.</param>
    /// <param name="text">The text.</param>
    /// <param name="position">Where the digits start.</param>
    /// <param name="digits">How many digits are in a row.</param>
    /// <param name="set">The current code set, changed to C.</param>
    /// <returns>The position after the digits that were added.</returns>
    private static int AddDigitPairs(ref Code128Values values, string text, int position, int digits, ref char set)
    {
        if (set != 'C' && (digits & 1) == 1)
        {
            // An odd run puts its first digit in the set already in use, or in set B at the start, so the rest pair up.
            if (set == ' ')
            {
                Switch(ref values, ref set, 'B');
            }

            values.Add(text[position] - FirstPrintable);
            position++;
            digits--;
        }

        if (set != 'C')
        {
            Switch(ref values, ref set, 'C');
        }

        var end = position + (digits & ~1);
        while (position < end)
        {
            values.Add(((text[position] - '0') * Ten) + (text[position + 1] - '0'));
            position += DigitPair;
        }

        return position;
    }

    /// <summary>Switches code set, or starts one when none has been chosen yet.</summary>
    /// <param name="values">The values so far.</param>
    /// <param name="set">The current code set, or a space before the first.</param>
    /// <param name="wanted">The set to move to.</param>
    private static void Switch(ref Code128Values values, ref char set, char wanted)
    {
        if (set == ' ')
        {
            values.Add(wanted switch { 'A' => StartA, 'C' => StartC, _ => StartB });
        }
        else
        {
            values.Add(wanted switch { 'A' => SwitchToA, 'C' => SwitchToC, _ => SwitchToB });
        }

        set = wanted;
    }

    /// <summary>Determines whether a printable character is in both code set A and code set B.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for characters 32 to 95, which both sets hold.</returns>
    private static bool IsInBothSets(char character) => character < FirstOnlyInB;

    /// <summary>Gets the value of a character in code set A.</summary>
    /// <param name="character">The character: a control character or one from 32 to 95.</param>
    /// <returns>The value.</returns>
    private static int GetValueInA(char character) => character < FirstPrintable ? character + ControlOffset : character - FirstPrintable;
}
