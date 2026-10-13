// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Resolves the embedding level of each character of a line of text (Unicode Standard Annex #9, rules W1 to W7, N1, N2,
/// I1, I2 and L1). Explicit embeddings, overrides and isolates are not interpreted: a line is one paragraph at one base level.
/// </summary>
internal static class Bidi
{
    /// <summary>The base level of left-to-right text.</summary>
    internal const byte LeftToRight = 0;

    /// <summary>The base level of right-to-left text.</summary>
    internal const byte RightToLeft = 1;

    /// <summary>The levels a number rises above an even base level (rule I1).</summary>
    private const int NumberRise = 2;

    /// <summary>Finds the base direction of text from its first strong character.</summary>
    /// <param name="codePoints">The text.</param>
    /// <returns>The base level: 1 when the first letter reads right to left, otherwise 0.</returns>
    internal static byte FirstStrongLevel(ReadOnlySpan<int> codePoints)
    {
        foreach (var codePoint in codePoints)
        {
            var type = BidiClasses.Of(codePoint);
            if (type == BidiClass.L)
            {
                return LeftToRight;
            }

            if (type is BidiClass.R or BidiClass.AL)
            {
                return RightToLeft;
            }
        }

        return LeftToRight;
    }

    /// <summary>Resolves the levels of a line.</summary>
    /// <param name="codePoints">The text.</param>
    /// <param name="baseLevel">The paragraph level, 0 or 1.</param>
    /// <param name="levels">Receives the level of each character; as long as <paramref name="codePoints"/>.</param>
    internal static void ResolveLevels(ReadOnlySpan<int> codePoints, byte baseLevel, Span<byte> levels)
    {
        var rented = ArrayPool<BidiClass>.Shared.Rent(codePoints.Length + codePoints.Length);
        try
        {
            var original = rented.AsSpan(0, codePoints.Length);
            var work = rented.AsSpan(codePoints.Length, codePoints.Length);
            for (var i = 0; i < work.Length; i++)
            {
                original[i] = BidiClasses.Of(codePoints[i]);
            }

            original.CopyTo(work);
            ResolveWeak(work, baseLevel);
            ResolveNeutral(work, baseLevel);
            AssignLevels(work, baseLevel, levels);
            ResetTrailingSpace(original, baseLevel, levels);
        }
        finally
        {
            ArrayPool<BidiClass>.Shared.Return(rented);
        }
    }

    /// <summary>Gets the strong type at the start of a line, which is the direction of the paragraph.</summary>
    /// <param name="baseLevel">The paragraph level.</param>
    /// <returns><see cref="BidiClass.R"/> or <see cref="BidiClass.L"/>.</returns>
    private static BidiClass StartType(byte baseLevel) => baseLevel == RightToLeft ? BidiClass.R : BidiClass.L;

    /// <summary>Applies the weak type rules W1 to W7.</summary>
    /// <param name="types">The types, changed in place.</param>
    /// <param name="baseLevel">The paragraph level, which gives the type at the start of the line.</param>
    private static void ResolveWeak(Span<BidiClass> types, byte baseLevel)
    {
        var startType = StartType(baseLevel);
        ResolveMarks(types, startType);
        ResolveArabicDigits(types, startType);
        ResolveSeparators(types);
        ResolveTerminators(types);
        NeutraliseSeparators(types);
        ResolveEuropeanDigits(types, startType);
    }

    /// <summary>Applies W1: a non-spacing mark takes the type of the character before it.</summary>
    /// <param name="types">The types, changed in place.</param>
    /// <param name="startType">The type at the start of the line.</param>
    private static void ResolveMarks(Span<BidiClass> types, BidiClass startType)
    {
        var previous = startType;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == BidiClass.NSM)
            {
                types[i] = previous;
            }
            else if (types[i] != BidiClass.BN)
            {
                previous = types[i];
            }
        }
    }

    /// <summary>Applies W2 and W3: a European digit after an Arabic letter is an Arabic digit, and Arabic letters then read as R.</summary>
    /// <param name="types">The types, changed in place.</param>
    /// <param name="startType">The type at the start of the line.</param>
    private static void ResolveArabicDigits(Span<BidiClass> types, BidiClass startType)
    {
        var strong = startType;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is BidiClass.L or BidiClass.R or BidiClass.AL)
            {
                strong = types[i];
            }
            else if (types[i] == BidiClass.EN && strong == BidiClass.AL)
            {
                types[i] = BidiClass.AN;
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            types[i] = types[i] == BidiClass.AL ? BidiClass.R : types[i];
        }
    }

    /// <summary>Applies W4: a single separator between two numbers of one kind joins them.</summary>
    /// <param name="types">The types, changed in place.</param>
    private static void ResolveSeparators(Span<BidiClass> types)
    {
        for (var i = 1; i < types.Length - 1; i++)
        {
            var before = types[i - 1];
            var after = types[i + 1];
            if (types[i] == BidiClass.ES && before == BidiClass.EN && after == BidiClass.EN)
            {
                types[i] = BidiClass.EN;
            }
            else if (types[i] == BidiClass.CS && before == after && before is BidiClass.EN or BidiClass.AN)
            {
                types[i] = before;
            }
        }
    }

    /// <summary>Applies W5: terminators next to European digits become digits, and the rest become neutral.</summary>
    /// <param name="types">The types, changed in place.</param>
    private static void ResolveTerminators(Span<BidiClass> types)
    {
        var i = 0;
        while (i < types.Length)
        {
            if (types[i] != BidiClass.ET)
            {
                i++;
                continue;
            }

            var end = i;
            while (end < types.Length && types[end] == BidiClass.ET)
            {
                end++;
            }

            var touchesDigit = (i > 0 && types[i - 1] == BidiClass.EN) || (end < types.Length && types[end] == BidiClass.EN);
            types[i..end].Fill(touchesDigit ? BidiClass.EN : BidiClass.ON);
            i = end;
        }
    }

    /// <summary>Applies W6: separators that were not joined to numbers become neutral.</summary>
    /// <param name="types">The types, changed in place.</param>
    private static void NeutraliseSeparators(Span<BidiClass> types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            types[i] = types[i] is BidiClass.ES or BidiClass.CS ? BidiClass.ON : types[i];
        }
    }

    /// <summary>Applies W7: a European digit after a left-to-right letter reads as left-to-right.</summary>
    /// <param name="types">The types, changed in place.</param>
    /// <param name="startType">The type at the start of the line.</param>
    private static void ResolveEuropeanDigits(Span<BidiClass> types, BidiClass startType)
    {
        var strong = startType;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is BidiClass.L or BidiClass.R)
            {
                strong = types[i];
            }
            else if (types[i] == BidiClass.EN && strong == BidiClass.L)
            {
                types[i] = BidiClass.L;
            }
        }
    }

    /// <summary>Applies N1 and N2: a run of neutrals takes the direction of the text around it when both sides agree, otherwise the paragraph direction.</summary>
    /// <param name="types">The types, changed in place.</param>
    /// <param name="baseLevel">The paragraph level.</param>
    private static void ResolveNeutral(Span<BidiClass> types, byte baseLevel)
    {
        var embedding = StartType(baseLevel);
        var i = 0;
        while (i < types.Length)
        {
            if (!IsNeutral(types[i]))
            {
                i++;
                continue;
            }

            var end = i;
            while (end < types.Length && IsNeutral(types[end]))
            {
                end++;
            }

            var before = i == 0 ? embedding : Direction(types[i - 1]);
            var after = end == types.Length ? embedding : Direction(types[end]);
            types[i..end].Fill(before == after ? before : embedding);
            i = end;
        }
    }

    /// <summary>Determines whether a type is a neutral for rules N1 and N2.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for neutrals.</returns>
    private static bool IsNeutral(BidiClass type) => type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or BidiClass.BN;

    /// <summary>Gets the direction a strong or number type counts as in rule N1: numbers count as right to left.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see cref="BidiClass.L"/> or <see cref="BidiClass.R"/>.</returns>
    private static BidiClass Direction(BidiClass type) => type == BidiClass.L ? BidiClass.L : BidiClass.R;

    /// <summary>Applies I1 and I2: sets the level of each character from its resolved type.</summary>
    /// <param name="types">The resolved types.</param>
    /// <param name="baseLevel">The paragraph level.</param>
    /// <param name="levels">Receives the levels.</param>
    private static void AssignLevels(ReadOnlySpan<BidiClass> types, byte baseLevel, Span<byte> levels)
    {
        for (var i = 0; i < types.Length; i++)
        {
            levels[i] = (byte)(baseLevel + (baseLevel == LeftToRight ? RiseFromEven(types[i]) : RiseFromOdd(types[i])));
        }
    }

    /// <summary>Gets how many levels a type rises above an even paragraph level (rule I1).</summary>
    /// <param name="type">The type.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int RiseFromEven(BidiClass type) => type switch
    {
        BidiClass.R => 1,
        BidiClass.AN or BidiClass.EN => NumberRise,
        _ => 0,
    };

    /// <summary>Gets how many levels a type rises above an odd paragraph level (rule I2).</summary>
    /// <param name="type">The type.</param>
    /// <returns>0 or 1.</returns>
    private static int RiseFromOdd(BidiClass type) => type is BidiClass.L or BidiClass.EN or BidiClass.AN ? 1 : 0;

    /// <summary>Applies L1: white space at the end of the line takes the paragraph level.</summary>
    /// <param name="original">The types before resolution.</param>
    /// <param name="baseLevel">The paragraph level.</param>
    /// <param name="levels">The levels, changed in place.</param>
    private static void ResetTrailingSpace(ReadOnlySpan<BidiClass> original, byte baseLevel, Span<byte> levels)
    {
        for (var i = original.Length - 1; i >= 0; i--)
        {
            if (original[i] is not (BidiClass.WS or BidiClass.BN))
            {
                return;
            }

            levels[i] = baseLevel;
        }
    }
}
