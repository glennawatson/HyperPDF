// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// The code words of Recommendation T.4 (tables 2 and 3 for runs, table 4 for two-dimensional modes) and Recommendation
/// T.6, and the writing of a run as make-up and terminating codes. Codes are held as the bit strings the recommendations
/// print.
/// </summary>
internal static class CcittCodeTables
{
    /// <summary>The index of the pass code in <see cref="Modes"/>.</summary>
    internal const int PassMode = 0;

    /// <summary>The index of the horizontal code in <see cref="Modes"/>.</summary>
    internal const int HorizontalMode = 1;

    /// <summary>The index of the V0 code in <see cref="Modes"/>; VL3 to VR3 sit around it.</summary>
    internal const int VerticalZero = 5;

    /// <summary>The largest vertical-mode offset.</summary>
    internal const int MaxVertical = 3;

    /// <summary>The end-of-line code: eleven zeros and a one.</summary>
    internal static readonly CcittCode EndOfLine = new(1, 12);

    /// <summary>The longest run a make-up code covers.</summary>
    private const int MaxMakeup = 2560;

    /// <summary>The step between make-up codes.</summary>
    private const int MakeupStep = 64;

    /// <summary>The longest run the colour-specific make-up codes cover.</summary>
    private const int ColorMakeupLimit = 1728;

    /// <summary>The run the first extended make-up code covers.</summary>
    private const int ExtendedStart = 1792;

    /// <summary>The white terminating codes, runs 0 to 63.</summary>
    private static readonly CcittCode[] WhiteTerminating = Parse(
        "00110101 000111 0111 1000 1011 1100 1110 1111 10011 10100 00111 01000 001000 000011 110100 110101 "u8
        + "101010 101011 0100111 0001100 0001000 0010111 0000011 0000100 0101000 0101011 0010011 0100100 "u8
        + "0011000 00000010 00000011 00011010 00011011 00010010 00010011 00010100 00010101 00010110 00010111 "u8
        + "00101000 00101001 00101010 00101011 00101100 00101101 00000100 00000101 00001010 00001011 01010010 "u8
        + "01010011 01010100 01010101 00100100 00100101 01011000 01011001 01011010 01011011 01001010 01001011 "u8
        + "00110010 00110011 00110100"u8);

    /// <summary>The white make-up codes, runs 64 to 1728.</summary>
    private static readonly CcittCode[] WhiteMakeup = Parse(
        "11011 10010 010111 0110111 00110110 00110111 01100100 01100101 01101000 01100111 011001100 011001101 "u8
        + "011010010 011010011 011010100 011010101 011010110 011010111 011011000 011011001 011011010 011011011 "u8
        + "010011000 010011001 010011010 011000 010011011"u8);

    /// <summary>The black terminating codes, runs 0 to 63.</summary>
    private static readonly CcittCode[] BlackTerminating = Parse(
        "0000110111 010 11 10 011 0011 0010 00011 000101 000100 0000100 0000101 0000111 00000100 00000111 "u8
        + "000011000 0000010111 0000011000 0000001000 00001100111 00001101000 00001101100 00000110111 "u8
        + "00000101000 00000010111 00000011000 000011001010 000011001011 000011001100 000011001101 "u8
        + "000001101000 000001101001 000001101010 000001101011 000011010010 000011010011 000011010100 "u8
        + "000011010101 000011010110 000011010111 000001101100 000001101101 000011011010 000011011011 "u8
        + "000001010100 000001010101 000001010110 000001010111 000001100100 000001100101 000001010010 "u8
        + "000001010011 000000100100 000000110111 000000111000 000000100111 000000101000 000001011000 "u8
        + "000001011001 000000101011 000000101100 000001011010 000001100110 000001100111"u8);

    /// <summary>The black make-up codes, runs 64 to 1728.</summary>
    private static readonly CcittCode[] BlackMakeup = Parse(
        "0000001111 000011001000 000011001001 000001011011 000000110011 000000110100 000000110101 "u8
        + "0000001101100 0000001101101 0000001001010 0000001001011 0000001001100 0000001001101 "u8
        + "0000001110010 0000001110011 0000001110100 0000001110101 0000001110110 0000001110111 "u8
        + "0000001010010 0000001010011 0000001010100 0000001010101 0000001011010 0000001011011 "u8
        + "0000001100100 0000001100101"u8);

    /// <summary>The extended make-up codes shared by both colours, runs 1792 to 2560.</summary>
    private static readonly CcittCode[] ExtendedMakeup = Parse(
        "00000001000 00000001100 00000001101 000000010010 000000010011 000000010100 000000010101 000000010110 "u8
        + "000000010111 000000011100 000000011101 000000011110 000000011111"u8);

    /// <summary>Gets the mode codes: pass, horizontal, then vertical left 3, 2, 1, zero and right 1, 2, 3.</summary>
    internal static CcittCode[] Modes { get; } = Parse("0001 001 0000010 000010 010 1 011 000011 0000011"u8);

    /// <summary>Writes one run: make-up codes, then a terminating code.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="run">The run length.</param>
    /// <param name="black">Whether the run is black.</param>
    internal static void WriteRun(ref CcittBitWriter bits, int run, bool black)
    {
        while (run > MaxMakeup)
        {
            bits.Write(ExtendedMakeup[^1]);
            run -= MaxMakeup;
        }

        if (run >= MakeupStep)
        {
            var makeup = run / MakeupStep * MakeupStep;
            bits.Write(makeup <= ColorMakeupLimit
                ? (black ? BlackMakeup : WhiteMakeup)[(makeup / MakeupStep) - 1]
                : ExtendedMakeup[(makeup - ExtendedStart) / MakeupStep]);
            run -= makeup;
        }

        bits.Write((black ? BlackTerminating : WhiteTerminating)[run]);
    }

    /// <summary>Turns codes written as bit strings separated by spaces into code values and lengths.</summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The parsed codes, in order.</returns>
    private static CcittCode[] Parse(ReadOnlySpan<byte> codes)
    {
        var parsed = new List<CcittCode>();
        foreach (var range in codes.Split((byte)' '))
        {
            var code = codes[range];
            var value = 0;
            foreach (var digit in code)
            {
                value = (value << 1) | (digit - (byte)'0');
            }

            parsed.Add(new(value, code.Length));
        }

        return [.. parsed];
    }
}
