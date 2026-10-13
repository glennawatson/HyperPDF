// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Cursive joining of Arabic letters (Unicode Standard, "Arabic" chapter, joining types in ArabicShaping.txt): each letter
/// joins the letters before and after it according to its joining type, and so takes its isolated, initial, medial or
/// final form, which the font's isol, init, medi and fina features then substitute.
/// </summary>
internal static class ArabicJoining
{
    /// <summary>The first Arabic block character.</summary>
    private const int ArabicStart = 0x0600;

    /// <summary>The last character of the Arabic block.</summary>
    private const int ArabicEnd = 0x06FF;

    /// <summary>The tatweel, which joins on both sides.</summary>
    private const int Tatweel = 0x0640;

    /// <summary>The zero width joiner, which makes its neighbours join it.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>The zero width non-joiner, which stops joining.</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>The Arabic Supplement block start.</summary>
    private const int SupplementStart = 0x0750;

    /// <summary>The Arabic Supplement block end.</summary>
    private const int SupplementEnd = 0x077F;

    /// <summary>Letters that join only to the letter before them (joining type R) in the Arabic block.</summary>
    private static readonly int[] RightJoining =
    [
        0x0622, 0x0623, 0x0624, 0x0625, 0x0627, 0x0629, 0x062F, 0x0630, 0x0631, 0x0632, 0x0648, 0x0671, 0x0672, 0x0673, 0x0675, 0x0676, 0x0677,
        0x0688, 0x0689, 0x068A, 0x068B, 0x068C, 0x068D, 0x068E, 0x068F, 0x0690, 0x0691, 0x0692, 0x0693, 0x0694, 0x0695, 0x0696, 0x0697, 0x0698, 0x0699,
        0x06C0, 0x06C3, 0x06C4, 0x06C5, 0x06C6, 0x06C7, 0x06C8, 0x06C9, 0x06CA, 0x06CB, 0x06CD, 0x06CF, 0x06D2, 0x06D3, 0x06D5, 0x06EE, 0x06EF,
    ];

    /// <summary>Letters of the Arabic block that do not join at all (joining type U): hamza and the small high letters.</summary>
    private static readonly int[] NonJoining = [0x0621, 0x0674, 0x06E5, 0x06E6];

    /// <summary>Letters of the Arabic Supplement block that join only to the letter before them.</summary>
    private static readonly int[] SupplementRightJoining = [0x0759, 0x075A, 0x075B, 0x076B, 0x076C, 0x0771, 0x0773, 0x0774, 0x0778, 0x0779];

    /// <summary>Gets the joining type of a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The joining type.</returns>
    internal static JoiningType TypeOf(int codePoint) => codePoint switch
    {
        Tatweel or ZeroWidthJoiner => JoiningType.Causing,
        ZeroWidthNonJoiner => JoiningType.None,
        _ => TypeOfLetter(codePoint),
    };

    /// <summary>Assigns each character of a run the form it takes.</summary>
    /// <param name="codePoints">The characters of the run, in logical order.</param>
    /// <param name="masks">Receives the feature mask of each character: the bit of the form it takes, or zero.</param>
    internal static void AssignForms(ReadOnlySpan<int> codePoints, Span<uint> masks)
    {
        var previous = JoiningType.None;
        var previousIndex = -1;
        for (var i = 0; i < codePoints.Length; i++)
        {
            var type = TypeOf(codePoints[i]);
            masks[i] = 0;
            if (type == JoiningType.Transparent)
            {
                continue;
            }

            var joinsPrevious = JoinsForward(previous) && JoinsBackward(type);
            if (joinsPrevious && previousIndex >= 0)
            {
                masks[previousIndex] = JoinNext(masks[previousIndex]);
            }

            masks[i] = FormMask(type, joinsPrevious);
            previous = type;
            previousIndex = i;
        }
    }

    /// <summary>Determines whether a joining type reaches toward the letter after it.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for dual-joining, left-joining and join-causing characters.</returns>
    private static bool JoinsForward(JoiningType type) => type is JoiningType.Dual or JoiningType.Left or JoiningType.Causing;

    /// <summary>Determines whether a joining type reaches toward the letter before it.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for dual-joining, right-joining and join-causing characters.</returns>
    private static bool JoinsBackward(JoiningType type) => type is JoiningType.Dual or JoiningType.Right or JoiningType.Causing;

    /// <summary>Gets the form of a letter that joins the one before it.</summary>
    /// <param name="type">The letter's joining type.</param>
    /// <param name="joinsPrevious">Whether it joins the letter before it.</param>
    /// <returns>The mask of the form before the next letter is known; zero for a letter that never joins.</returns>
    private static uint FormMask(JoiningType type, bool joinsPrevious)
    {
        if (type is not (JoiningType.Dual or JoiningType.Right or JoiningType.Causing))
        {
            return type == JoiningType.Left ? ShapeMasks.Initial : 0;
        }

        return joinsPrevious ? ShapeMasks.Final : ShapeMasks.Isolated;
    }

    /// <summary>Changes the form of a letter when the letter after it joins it: a final form becomes medial, an isolated one initial.</summary>
    /// <param name="mask">The letter's mask so far.</param>
    /// <returns>The new mask.</returns>
    private static uint JoinNext(uint mask) => mask switch
    {
        ShapeMasks.Final => ShapeMasks.Medial,
        ShapeMasks.Isolated => ShapeMasks.Initial,
        _ => mask,
    };

    /// <summary>Gets the joining type of an Arabic letter or mark.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The joining type.</returns>
    private static JoiningType TypeOfLetter(int codePoint)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return JoiningType.Transparent;
        }

        if (codePoint is >= ArabicStart and <= ArabicEnd)
        {
            return OfBlock(codePoint, category);
        }

        if (codePoint is >= SupplementStart and <= SupplementEnd)
        {
            return Array.IndexOf(SupplementRightJoining, codePoint) >= 0 ? JoiningType.Right : JoiningType.Dual;
        }

        return JoiningType.None;
    }

    /// <summary>Gets the joining type of a character of the Arabic block.</summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="category">The character's general category.</param>
    /// <returns>The joining type.</returns>
    private static JoiningType OfBlock(int codePoint, UnicodeCategory category)
    {
        if (category is not (UnicodeCategory.OtherLetter or UnicodeCategory.ModifierLetter) || Array.IndexOf(NonJoining, codePoint) >= 0)
        {
            return JoiningType.None;
        }

        // The block's letters are dual-joining unless they are listed as joining one side or not at all (ArabicShaping.txt).
        return Array.BinarySearch(RightJoining, codePoint) >= 0 ? JoiningType.Right : JoiningType.Dual;
    }
}
