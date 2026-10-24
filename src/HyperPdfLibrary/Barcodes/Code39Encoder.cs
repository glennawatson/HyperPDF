// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes text as Code 39: each character is five bars and four spaces, three of them wide.</summary>
internal static class Code39Encoder
{
    /// <summary>The width of a wide bar or space, in modules.</summary>
    private const int Wide = 3;

    /// <summary>The characters in the order of <see cref="Patterns"/>; the last, <c>*</c>, starts and stops a code.</summary>
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*";

    /// <summary>The pattern of each character in the alphabet: <c>n</c> for a narrow element and <c>w</c> for a wide one.</summary>
    private static readonly string[] Patterns =
    [
        "nnnwwnwnn", "wnnwnnnnw", "nnwwnnnnw", "wnwwnnnnn", "nnnwwnnnw", "wnnwwnnnn", "nnwwwnnnn", "nnnwnnwnw", "wnnwnnwnn", "nnwwnnwnn",
        "wnnnnwnnw", "nnwnnwnnw", "wnwnnwnnn", "nnnnwwnnw", "wnnnwwnnn", "nnwnwwnnn", "nnnnnwwnw", "wnnnnwwnn", "nnwnnwwnn", "nnnnwwwnn",
        "wnnnnnnww", "nnwnnnnww", "wnwnnnnwn", "nnnnwnnww", "wnnnwnnwn", "nnwnwnnwn", "nnnnnnwww", "wnnnnnwwn", "nnwnnnwwn", "nnnnwnwwn",
        "wwnnnnnnw", "nwwnnnnnw", "wwwnnnnnn", "nwnnwnnnw", "wwnnwnnnn", "nwwnwnnnn", "nwnnnnwnw", "wwnnnnwnn", "nwwnnnwnn", "nwnwnwnnn",
        "nwnwnnnwn", "nwnnnwnwn", "nnnwnwnwn", "nwnnwnwnn",
    ];

    /// <summary>Encodes text.</summary>
    /// <param name="text">The text: capital letters, digits, space and <c>- . $ / + %</c>.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character Code 39 cannot hold.</exception>
    internal static PdfBarcode Encode(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        LinearModules modules = default;
        AddCharacter(ref modules, Alphabet.Length - 1);
        foreach (var character in text)
        {
            var index = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0 || index == Alphabet.Length - 1)
            {
                throw new ArgumentException($"Code 39 cannot hold the character '{character}'.", nameof(text));
            }

            modules.AddRun(1);
            AddCharacter(ref modules, index);
        }

        modules.AddRun(1);
        AddCharacter(ref modules, Alphabet.Length - 1);
        return modules.ToBarcode(PdfBarcodeSymbology.Code39, text);
    }

    /// <summary>Adds the bars and spaces of one character.</summary>
    /// <param name="modules">The modules so far.</param>
    /// <param name="index">The character's place in the alphabet.</param>
    private static void AddCharacter(ref LinearModules modules, int index)
    {
        foreach (var element in Patterns[index])
        {
            modules.AddRun(element == 'w' ? Wide : 1);
        }
    }
}
