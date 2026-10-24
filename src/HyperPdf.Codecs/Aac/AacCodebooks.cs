// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>The decoding tables of the AAC codebooks, built once and never changed.</summary>
internal static class AacCodebooks
{
    /// <summary>The codebook number that marks a band with no coefficients.</summary>
    internal const int Zero = 0;

    /// <summary>The first codebook number that is not a spectral codebook.</summary>
    internal const int FirstSpecial = 12;

    /// <summary>The codebook number that marks a band of substituted noise.</summary>
    internal const int Noise = 13;

    /// <summary>The codebook number that marks an intensity stereo band out of phase.</summary>
    internal const int IntensityOutOfPhase = 14;

    /// <summary>The codebook number that marks an intensity stereo band in phase.</summary>
    internal const int IntensityInPhase = 15;

    /// <summary>The codebook whose values above 15 continue in escape codes.</summary>
    internal const int Escape = 11;

    /// <summary>The first codebook whose values are coded in pairs.</summary>
    internal const int FirstPairBook = 5;

    /// <summary>The last of the two codebooks of four signed values, which come before the unsigned ones.</summary>
    internal const int LastSignedQuadBook = 2;

    /// <summary>The first of the two signed pair codebooks.</summary>
    internal const int FirstSignedPairBook = 5;

    /// <summary>The last of the two signed pair codebooks.</summary>
    internal const int LastSignedPairBook = 6;

    /// <summary>The number of values one symbol of the first four codebooks stands for.</summary>
    private const int QuadSize = 4;

    /// <summary>The number of values one symbol of the pair codebooks stands for.</summary>
    private const int PairSize = 2;

    /// <summary>The spectral codebooks, by number; the entry for number 0 is empty.</summary>
    private static readonly PrefixCodeTable[] SpectralBooks = BuildSpectral();

    /// <summary>The scalefactor codebook.</summary>
    private static readonly PrefixCodeTable ScalefactorBook = new(AacCodebookData.ScalefactorLengths, AacCodebookData.ScalefactorCodes);

    /// <summary>The values a symbol of each spectral codebook stands for, symbol by symbol; the entry for number 0 is empty.</summary>
    private static readonly sbyte[][] ValueTables = BuildValues();

    /// <summary>Gets what each codebook's symbol is split by to give its values: the count of values each digit can take.</summary>
    private static ReadOnlySpan<byte> Modulus => [0x0, 0x3, 0x3, 0x3, 0x3, 0x9, 0x9, 0x8, 0x8, 0xD, 0xD, 0x11];

    /// <summary>Gets what is taken from each digit of a symbol to give a signed value.</summary>
    private static ReadOnlySpan<byte> Bias => [0x0, 0x1, 0x1, 0x0, 0x0, 0x4, 0x4, 0x0, 0x0, 0x0, 0x0, 0x0];

    /// <summary>Gets the number of values one symbol of a spectral codebook stands for.</summary>
    /// <param name="book">The codebook number, from 1 to 11.</param>
    /// <returns>4 for the first four codebooks, otherwise 2.</returns>
    internal static int Dimension(int book) => book < FirstPairBook ? QuadSize : PairSize;

    /// <summary>Gets the values the symbols of a spectral codebook stand for.</summary>
    /// <param name="book">The codebook number, from 1 to 11.</param>
    /// <returns>The values of symbol 0, then of symbol 1, and so on; for unsigned codebooks they are magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sbyte[] Values(int book) => ValueTables[book];

    /// <summary>Gets the table of a spectral codebook.</summary>
    /// <param name="book">The codebook number, from 1 to 11.</param>
    /// <returns>The table.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PrefixCodeTable Spectral(int book) => SpectralBooks[book];

    /// <summary>Gets the scalefactor codebook.</summary>
    /// <returns>The table.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PrefixCodeTable Scalefactor() => ScalefactorBook;

    /// <summary>Builds the value tables of the eleven spectral codebooks.</summary>
    /// <returns>The tables, with an empty slot first so a codebook number indexes its table.</returns>
    private static sbyte[][] BuildValues()
    {
        var result = new sbyte[Modulus.Length][];
        result[0] = [];
        for (var book = 1; book < Modulus.Length; book++)
        {
            var dimension = Dimension(book);
            var modulus = Modulus[book];
            var symbols = 1;
            for (var digit = 0; digit < dimension; digit++)
            {
                symbols *= modulus;
            }

            var values = new sbyte[symbols * dimension];
            for (var symbol = 0; symbol < symbols; symbol++)
            {
                var rest = symbol;
                for (var digit = dimension - 1; digit >= 0; digit--)
                {
                    values[(symbol * dimension) + digit] = (sbyte)((rest % modulus) - Bias[book]);
                    rest /= modulus;
                }
            }

            result[book] = values;
        }

        return result;
    }

    /// <summary>Builds the tables of the eleven spectral codebooks.</summary>
    /// <returns>The tables, with an empty slot first so a codebook number indexes its table.</returns>
    private static PrefixCodeTable[] BuildSpectral() =>
    [
        null!,
        new(AacCodebookData.Book1Lengths, AacCodebookData.Book1Codes),
        new(AacCodebookData.Book2Lengths, AacCodebookData.Book2Codes),
        new(AacCodebookData.Book3Lengths, AacCodebookData.Book3Codes),
        new(AacCodebookData.Book4Lengths, AacCodebookData.Book4Codes),
        new(AacCodebookData.Book5Lengths, AacCodebookData.Book5Codes),
        new(AacCodebookData.Book6Lengths, AacCodebookData.Book6Codes),
        new(AacCodebookData.Book7Lengths, AacCodebookData.Book7Codes),
        new(AacCodebookData.Book8Lengths, AacCodebookData.Book8Codes),
        new(AacCodebookData.Book9Lengths, AacCodebookData.Book9Codes),
        new(AacCodebookData.Book10Lengths, AacCodebookData.Book10Codes),
        new(AacCodebookData.Book11Lengths, AacCodebookData.Book11Codes),
    ];
}
