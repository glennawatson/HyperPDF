// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>
/// Implicit bidirectional levels and reordering from the Unicode Bidirectional Algorithm (Unicode Standard Annex #9):
/// weak type rules W1 to W7, neutral rules N1 and N2, implicit levels I1 and I2 and the reordering rule L2. Explicit
/// embeddings, overrides and isolates (rules X1 to X10) are treated as boundary neutrals, and bracket pairs (N0) are not
/// paired. A text line is given in the order the glyphs sit on the page; resolving levels as if that order were the
/// character order and applying L2 returns the logical order, because L2 reverses runs and is its own inverse for
/// runs of one level.
/// </summary>
internal static class TextBidi
{
    /// <summary>The first explicit formatting class.</summary>
    private const TextBidiClass FirstExplicit = TextBidiClass.LRE;

    /// <summary>The last explicit formatting class.</summary>
    private const TextBidiClass LastExplicit = TextBidiClass.PDI;

    /// <summary>The levels added to a number in a left-to-right base paragraph.</summary>
    private const int NumberLevelStep = 2;

    /// <summary>Determines whether a line has any character that can change the order: right-to-left letters or Arabic numbers.</summary>
    /// <param name="classes">The classes of the line's characters.</param>
    /// <returns><see langword="true"/> when reordering may change the line.</returns>
    internal static bool NeedsReordering(ReadOnlySpan<TextBidiClass> classes)
    {
        foreach (var value in classes)
        {
            if (value is TextBidiClass.R or TextBidiClass.AL or TextBidiClass.AN)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a line has right-to-left letters and no left-to-right letters.</summary>
    /// <param name="classes">The classes of the line's characters.</param>
    /// <returns><see langword="true"/> for a line that reads right to left as a whole.</returns>
    internal static bool IsRightToLeftLine(ReadOnlySpan<TextBidiClass> classes)
    {
        var right = false;
        foreach (var value in classes)
        {
            if (value == TextBidiClass.L)
            {
                return false;
            }

            right |= value is TextBidiClass.R or TextBidiClass.AL;
        }

        return right;
    }

    /// <summary>Resolves the embedding level of each character.</summary>
    /// <param name="types">The classes of the characters; they are changed to the resolved types.</param>
    /// <param name="baseLevel">The paragraph level: 0 for left to right, 1 for right to left.</param>
    /// <param name="levels">Receives the level of each character.</param>
    internal static void ResolveLevels(Span<TextBidiClass> types, int baseLevel, Span<byte> levels)
    {
        var sos = baseLevel == 0 ? TextBidiClass.L : TextBidiClass.R;
        MarkExplicitAsBoundary(types);
        ApplyW1(types, sos);
        ApplyW2W3(types, sos);
        ApplyW4(types);
        ApplyW5(types);
        ApplyW6(types);
        ApplyW7(types, sos);
        ApplyN1N2(types, sos, baseLevel == 0 ? TextBidiClass.L : TextBidiClass.R);
        AssignLevels(types, baseLevel, levels);
    }

    /// <summary>Gets the display order of characters by reversing runs from the highest level down (rule L2).</summary>
    /// <param name="levels">The levels; they are rearranged along with the order.</param>
    /// <param name="order">Receives, for each display position, the index of the character shown there.</param>
    internal static void Reorder(Span<byte> levels, Span<int> order)
    {
        var highest = 0;
        var lowestOdd = int.MaxValue;
        for (var i = 0; i < levels.Length; i++)
        {
            order[i] = i;
            highest = Math.Max(highest, levels[i]);
            lowestOdd = (levels[i] & 1) != 0 ? Math.Min(lowestOdd, levels[i]) : lowestOdd;
        }

        for (var level = highest; level >= lowestOdd && level > 0; level--)
        {
            ReverseRuns(levels, order, level);
        }
    }

    /// <summary>Reverses each maximal run of positions at or above a level.</summary>
    /// <param name="levels">The levels by position.</param>
    /// <param name="order">The order by position.</param>
    /// <param name="level">The level.</param>
    private static void ReverseRuns(Span<byte> levels, Span<int> order, int level)
    {
        var i = 0;
        while (i < levels.Length)
        {
            if (levels[i] < level)
            {
                i++;
                continue;
            }

            var end = i;
            while (end < levels.Length && levels[end] >= level)
            {
                end++;
            }

            levels[i..end].Reverse();
            order[i..end].Reverse();
            i = end;
        }
    }

    /// <summary>Turns explicit formatting characters into boundary neutrals.</summary>
    /// <param name="types">The classes.</param>
    private static void MarkExplicitAsBoundary(Span<TextBidiClass> types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is >= FirstExplicit and <= LastExplicit)
            {
                types[i] = TextBidiClass.BN;
            }
        }
    }

    /// <summary>W1: a non-spacing mark takes the type of the character before it.</summary>
    /// <param name="types">The classes.</param>
    /// <param name="sos">The type at the start of the line.</param>
    private static void ApplyW1(Span<TextBidiClass> types, TextBidiClass sos)
    {
        var previous = sos;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == TextBidiClass.BN)
            {
                continue;
            }

            if (types[i] == TextBidiClass.NSM)
            {
                types[i] = previous;
            }

            previous = types[i];
        }
    }

    /// <summary>W2 and W3: European numbers after Arabic letters become Arabic numbers, then Arabic letters become right-to-left letters.</summary>
    /// <param name="types">The classes.</param>
    /// <param name="sos">The type at the start of the line.</param>
    private static void ApplyW2W3(Span<TextBidiClass> types, TextBidiClass sos)
    {
        var strong = sos;
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            if (type is TextBidiClass.L or TextBidiClass.R or TextBidiClass.AL)
            {
                strong = type;
            }
            else if (type == TextBidiClass.EN && strong == TextBidiClass.AL)
            {
                types[i] = TextBidiClass.AN;
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            types[i] = types[i] == TextBidiClass.AL ? TextBidiClass.R : types[i];
        }
    }

    /// <summary>W4: a single separator between two numbers of one kind joins them.</summary>
    /// <param name="types">The classes.</param>
    private static void ApplyW4(Span<TextBidiClass> types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is not (TextBidiClass.ES or TextBidiClass.CS))
            {
                continue;
            }

            var before = PreviousSolid(types, i);
            var after = NextSolid(types, i);
            if (before < 0 || after < 0 || types[before] != types[after])
            {
                continue;
            }

            var number = types[before];
            var joins = number == TextBidiClass.EN || (number == TextBidiClass.AN && types[i] == TextBidiClass.CS);
            types[i] = joins ? number : types[i];
        }
    }

    /// <summary>W5: terminators next to a European number become European numbers.</summary>
    /// <param name="types">The classes.</param>
    private static void ApplyW5(Span<TextBidiClass> types)
    {
        var i = 0;
        while (i < types.Length)
        {
            if (types[i] != TextBidiClass.ET)
            {
                i++;
                continue;
            }

            var end = i;
            while (end < types.Length && types[end] is TextBidiClass.ET or TextBidiClass.BN)
            {
                end++;
            }

            if (TouchesEuropeanNumber(types, i, end))
            {
                MakeEuropean(types[i..end]);
            }

            i = end;
        }
    }

    /// <summary>Determines whether a European number sits just before or just after a run.</summary>
    /// <param name="types">The classes.</param>
    /// <param name="start">The first position of the run.</param>
    /// <param name="end">The position after the run.</param>
    /// <returns><see langword="true"/> when a neighbour is a European number.</returns>
    private static bool TouchesEuropeanNumber(ReadOnlySpan<TextBidiClass> types, int start, int end)
    {
        var before = PreviousSolid(types, start);
        if (before >= 0 && types[before] == TextBidiClass.EN)
        {
            return true;
        }

        return end < types.Length && types[end] == TextBidiClass.EN;
    }

    /// <summary>Changes the terminators of a run to European numbers.</summary>
    /// <param name="run">The run.</param>
    private static void MakeEuropean(Span<TextBidiClass> run)
    {
        for (var i = 0; i < run.Length; i++)
        {
            run[i] = run[i] == TextBidiClass.ET ? TextBidiClass.EN : run[i];
        }
    }

    /// <summary>W6: separators and terminators that remain become other neutrals.</summary>
    /// <param name="types">The classes.</param>
    private static void ApplyW6(Span<TextBidiClass> types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is TextBidiClass.ES or TextBidiClass.ET or TextBidiClass.CS)
            {
                types[i] = TextBidiClass.ON;
            }
        }
    }

    /// <summary>W7: European numbers after left-to-right letters become left-to-right letters.</summary>
    /// <param name="types">The classes.</param>
    /// <param name="sos">The type at the start of the line.</param>
    private static void ApplyW7(Span<TextBidiClass> types, TextBidiClass sos)
    {
        var strong = sos;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is TextBidiClass.L or TextBidiClass.R)
            {
                strong = types[i];
            }
            else if (types[i] == TextBidiClass.EN && strong == TextBidiClass.L)
            {
                types[i] = TextBidiClass.L;
            }
        }
    }

    /// <summary>N1 and N2: neutrals between characters of one direction take it, and other neutrals take the paragraph direction.</summary>
    /// <param name="types">The classes.</param>
    /// <param name="sos">The type at the start of the line.</param>
    /// <param name="embedding">The paragraph direction.</param>
    private static void ApplyN1N2(Span<TextBidiClass> types, TextBidiClass sos, TextBidiClass embedding)
    {
        var i = 0;
        while (i < types.Length)
        {
            if (!IsNeutral(types[i]))
            {
                i++;
                continue;
            }

            var end = i;
            while (end < types.Length && (IsNeutral(types[end]) || types[end] == TextBidiClass.BN))
            {
                end++;
            }

            var previous = PreviousSolid(types, i);
            var before = previous < 0 ? sos : Direction(types[previous]);
            var after = end == types.Length ? sos : Direction(types[end]);
            FillNeutrals(types[i..end], before == after ? before : embedding);
            i = end;
        }
    }

    /// <summary>Sets the neutrals of a run to a direction.</summary>
    /// <param name="run">The run, which may hold boundary neutrals.</param>
    /// <param name="direction">The direction.</param>
    private static void FillNeutrals(Span<TextBidiClass> run, TextBidiClass direction)
    {
        for (var i = 0; i < run.Length; i++)
        {
            run[i] = IsNeutral(run[i]) ? direction : run[i];
        }
    }

    /// <summary>Gets the strong direction a resolved type counts as for neutral resolution: numbers count as right to left.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Left to right or right to left.</returns>
    private static TextBidiClass Direction(TextBidiClass type) => type == TextBidiClass.L ? TextBidiClass.L : TextBidiClass.R;

    /// <summary>Determines whether a type is a neutral or isolate formatting type.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for B, S, WS and ON.</returns>
    private static bool IsNeutral(TextBidiClass type) => type is TextBidiClass.B or TextBidiClass.S or TextBidiClass.WS or TextBidiClass.ON;

    /// <summary>I1 and I2: assigns the implicit level of each character.</summary>
    /// <param name="types">The resolved types.</param>
    /// <param name="baseLevel">The paragraph level.</param>
    /// <param name="levels">Receives the levels.</param>
    private static void AssignLevels(ReadOnlySpan<TextBidiClass> types, int baseLevel, Span<byte> levels)
    {
        var previous = baseLevel;
        for (var i = 0; i < types.Length; i++)
        {
            previous = types[i] == TextBidiClass.BN ? previous : LevelOf(types[i], baseLevel);
            levels[i] = (byte)previous;
        }
    }

    /// <summary>Gets the implicit level of a resolved type.</summary>
    /// <param name="type">The type.</param>
    /// <param name="baseLevel">The paragraph level.</param>
    /// <returns>The level.</returns>
    private static int LevelOf(TextBidiClass type, int baseLevel)
    {
        if (baseLevel == 0)
        {
            return type switch
            {
                TextBidiClass.R => 1,
                TextBidiClass.AN or TextBidiClass.EN => NumberLevelStep,
                _ => 0,
            };
        }

        return type is TextBidiClass.L or TextBidiClass.EN or TextBidiClass.AN ? baseLevel + 1 : baseLevel;
    }

    /// <summary>Finds the nearest character before an index that is not a boundary neutral.</summary>
    /// <param name="types">The types.</param>
    /// <param name="index">The index.</param>
    /// <returns>The position, or -1.</returns>
    private static int PreviousSolid(ReadOnlySpan<TextBidiClass> types, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (types[i] != TextBidiClass.BN)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the nearest character after an index that is not a boundary neutral.</summary>
    /// <param name="types">The types.</param>
    /// <param name="index">The index.</param>
    /// <returns>The position, or -1.</returns>
    private static int NextSolid(ReadOnlySpan<TextBidiClass> types, int index)
    {
        for (var i = index + 1; i < types.Length; i++)
        {
            if (types[i] != TextBidiClass.BN)
            {
                return i;
            }
        }

        return -1;
    }
}
