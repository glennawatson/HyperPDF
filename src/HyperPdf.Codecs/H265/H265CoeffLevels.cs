// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the levels of the significant coefficients of a sub-block: the greater-than-1 and greater-than-2 flags, the signs and the remaining magnitudes.</summary>
internal static class H265CoeffLevels
{
    /// <summary>The most greater-than-1 flags read in a sub-block.</summary>
    private const int MaxGreater1Flags = 8;

    /// <summary>The coefficients in a sub-block.</summary>
    private const int SubBlockCoefficients = 16;

    /// <summary>The base 2 logarithm of the side of a sub-block.</summary>
    private const int SubBlockLog2 = 2;

    /// <summary>The context sets of luma sub-blocks other than the first.</summary>
    private const int OuterLumaSet = 2;

    /// <summary>The contexts in a set of greater-than-1 flags.</summary>
    private const int Greater1SetSize = 4;

    /// <summary>The largest context of a greater-than-1 flag within its set.</summary>
    private const int MaxGreater1InSet = 3;

    /// <summary>The number of greater-than-1 contexts for luma, which chroma contexts follow.</summary>
    private const int ChromaGreater1Offset = 16;

    /// <summary>The number of greater-than-2 contexts for luma, which chroma contexts follow.</summary>
    private const int ChromaGreater2Offset = 4;

    /// <summary>The distance between the first and last significant position that lets a sign be hidden.</summary>
    private const int SignHidingDistance = 3;

    /// <summary>The level at which the remaining magnitude is read for a coefficient that has a greater-than-2 flag.</summary>
    private const int Greater2Threshold = 3;

    /// <summary>The level at which the remaining magnitude is read for a coefficient that has only a greater-than-1 flag.</summary>
    private const int Greater1Threshold = 2;

    /// <summary>The level at which the remaining magnitude is read for a coefficient that has no flag.</summary>
    private const int NoFlagThreshold = 1;

    /// <summary>The prefix length at which the remaining magnitude switches to an Exp-Golomb suffix.</summary>
    private const int RemainingPrefixLimit = 4;

    /// <summary>The largest Rice parameter when it does not adapt persistently.</summary>
    private const int MaxRice = 4;

    /// <summary>The factor of the threshold at which the Rice parameter grows.</summary>
    private const int RiceGrowFactor = 3;

    /// <summary>The divisor that turns a stored Rice state into a parameter.</summary>
    private const int StatDivisor = 4;

    /// <summary>The sign bit that is read next from the packed signs.</summary>
    private const int TopSignBit = 1 << (SubBlockCoefficients - 1);

    /// <summary>Reads the levels of a sub-block and stores them in the coefficient array.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="sub">Where the sub-block is.</param>
    /// <param name="significant">A mask with bit n set when the coefficient at scan position n is significant.</param>
    internal static void Read(ref CabacEngine engine, H265SliceContext context, H265ResidualState state, H265SubBlockPosition sub, int significant)
    {
        Span<int> level = stackalloc int[SubBlockCoefficients];
        var lastG1 = ReadGreater1(ref engine, state, sub.Index, significant, level);
        var lowest = BitOperations.TrailingZeroCount(significant);
        var hidden = IsSignHidden(context, state, significant);
        var signs = ReadSigns(ref engine, significant, hidden);
        var rice = InitialRice(context, state);
        var firstRemaining = true;
        var count = 0;
        var sum = 0;
        for (var n = SubBlockCoefficients - 1; n >= 0; n--)
        {
            if ((significant & (1 << n)) == 0)
            {
                continue;
            }

            var absolute = level[n];
            if (absolute == ThresholdOf(count, n, lastG1))
            {
                absolute = ResolveAbsolute(ref engine, context, state, absolute, ref rice, ref firstRemaining);
            }

            sum += absolute;
            var signHidden = n == lowest && hidden;
            var negative = signHidden ? (sum & 1) == 1 : (signs & TopSignBit) != 0;
            if (!signHidden)
            {
                signs <<= 1;
            }

            StoreLevel(context, state, sub, n, negative ? -absolute : absolute);
            count++;
        }
    }

    /// <summary>Tells whether the sign of the last coefficient of the sub-block is not coded but inferred.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="significant">The mask of significant coefficients.</param>
    /// <returns><see langword="true"/> when a sign is hidden.</returns>
    private static bool IsSignHidden(H265SliceContext context, H265ResidualState state, int significant) =>
        context.Pps.Flags.SignDataHiding && !state.NoSignHiding
        && BitOperations.Log2((uint)significant) - BitOperations.TrailingZeroCount(significant) > SignHidingDistance;

    /// <summary>Reads the coded sign bits of a sub-block, packed at the top of a 16 bit value.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="significant">The mask of significant coefficients.</param>
    /// <param name="hidden">Whether one sign is hidden.</param>
    /// <returns>The signs, the first at bit 15.</returns>
    private static int ReadSigns(ref CabacEngine engine, int significant, bool hidden)
    {
        var signCount = BitOperations.PopCount((uint)significant) - (hidden ? 1 : 0);
        return H265Bins.ReadBypass(ref engine, signCount) << (SubBlockCoefficients - signCount);
    }

    /// <summary>Gets the level at which a coefficient's remaining magnitude is read.</summary>
    /// <param name="count">The significant coefficients handled before this one.</param>
    /// <param name="position">The coefficient's scan position.</param>
    /// <param name="lastG1">The scan position of the coefficient that has a greater-than-2 flag.</param>
    /// <returns>The level.</returns>
    private static int ThresholdOf(int count, int position, int lastG1)
    {
        if (count >= MaxGreater1Flags)
        {
            return NoFlagThreshold;
        }

        return position == lastG1 ? Greater2Threshold : Greater1Threshold;
    }

    /// <summary>Reads the remaining magnitude of a coefficient and adapts the Rice parameter.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="absolute">The level so far.</param>
    /// <param name="rice">The Rice parameter, updated for the next coefficient.</param>
    /// <param name="firstRemaining">Whether this is the first remaining magnitude of the sub-block, cleared by the call.</param>
    /// <returns>The full magnitude.</returns>
    private static int ResolveAbsolute(ref CabacEngine engine, H265SliceContext context, H265ResidualState state, int absolute, ref int rice, ref bool firstRemaining)
    {
        var remaining = ReadRemaining(ref engine, rice);
        if (firstRemaining)
        {
            UpdateStat(context, state, remaining);
            firstRemaining = false;
        }

        var total = absolute + remaining;
        rice = NextRice(context, rice, total);
        return total;
    }

    /// <summary>Stores a coefficient and widens the extent of the block that holds coefficients.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="sub">Where the sub-block is.</param>
    /// <param name="position">The coefficient's scan position in the sub-block.</param>
    /// <param name="value">The signed level.</param>
    private static void StoreLevel(H265SliceContext context, H265ResidualState state, H265SubBlockPosition sub, int position, int value)
    {
        var scan = H265ResidualReader.Scan(state.ScanIdx, SubBlockLog2);
        var x = (sub.X << SubBlockLog2) + scan[position].X;
        var y = (sub.Y << SubBlockLog2) + scan[position].Y;
        context.Coefficients[(y << state.Log2Size) + x] = Math.Clamp(value, short.MinValue, short.MaxValue);
        state.MaxX = Math.Max(state.MaxX, x);
        state.MaxY = Math.Max(state.MaxY, y);
    }

    /// <summary>Reads the greater-than-1 and greater-than-2 flags of a sub-block and records each coefficient's base level.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="index">The sub-block's index in scan order.</param>
    /// <param name="significant">The mask of significant coefficients.</param>
    /// <param name="level">Receives the base level of each significant coefficient, by scan position.</param>
    /// <returns>The scan position of the first coefficient with a greater-than-1 flag set, or -1.</returns>
    private static int ReadGreater1(ref CabacEngine engine, H265ResidualState state, int index, int significant, scoped Span<int> level)
    {
        var chroma = state.CIdx > 0;
        var ctxSet = ContextSet(state, index);
        var greater1Ctx = 1;
        var read = 0;
        var first = -1;
        for (var n = SubBlockCoefficients - 1; n >= 0; n--)
        {
            if ((significant & (1 << n)) == 0)
            {
                continue;
            }

            level[n] = 1;
            if (read >= MaxGreater1Flags)
            {
                continue;
            }

            var ctx = (ctxSet * Greater1SetSize) + Math.Min(MaxGreater1InSet, greater1Ctx) + (chroma ? ChromaGreater1Offset : 0);
            var flag = engine.DecodeDecision(H265CabacContexts.Greater1 + ctx);
            read++;
            level[n] += flag;
            first = flag == 1 && first < 0 ? n : first;
            greater1Ctx = NextGreater1Context(greater1Ctx, flag);
        }

        state.Greater1Ctx = greater1Ctx;
        state.AnyCoded = true;
        return ReadGreater2(ref engine, ctxSet + (chroma ? ChromaGreater2Offset : 0), level, first);
    }

    /// <summary>Works out the context set of the greater-than-1 flags of a sub-block (9.3.4.2.6).</summary>
    /// <param name="state">The residual state.</param>
    /// <param name="index">The sub-block's index in scan order.</param>
    /// <returns>The set.</returns>
    private static int ContextSet(H265ResidualState state, int index)
    {
        var set = index == 0 || state.CIdx > 0 ? 0 : OuterLumaSet;
        return state.AnyCoded && state.Greater1Ctx == 0 ? set + 1 : set;
    }

    /// <summary>Works out the context of the next greater-than-1 flag.</summary>
    /// <param name="context">The context of the flag just read.</param>
    /// <param name="flag">The flag just read.</param>
    /// <returns>The next context.</returns>
    private static int NextGreater1Context(int context, int flag) => context == 0 || flag == 1 ? 0 : context + 1;

    /// <summary>Reads the greater-than-2 flag of the first coefficient whose greater-than-1 flag is set.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The context increment.</param>
    /// <param name="level">The base levels, updated.</param>
    /// <param name="first">The scan position of that coefficient, or -1.</param>
    /// <returns>The scan position, unchanged.</returns>
    private static int ReadGreater2(ref CabacEngine engine, int context, scoped Span<int> level, int first)
    {
        if (first >= 0)
        {
            level[first] += engine.DecodeDecision(H265CabacContexts.Greater2 + context);
        }

        return first;
    }

    /// <summary>Gets the Rice parameter the first remaining magnitude of a sub-block uses (9.3.3.11).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <returns>The parameter.</returns>
    private static int InitialRice(H265SliceContext context, H265ResidualState state) =>
        context.Sps.Range.PersistentRice ? context.StatCoeff[state.StatType] / StatDivisor : 0;

    /// <summary>Updates the persistent Rice state with the first remaining magnitude of a sub-block (9-23).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="remaining">The magnitude.</param>
    private static void UpdateStat(H265SliceContext context, H265ResidualState state, int remaining)
    {
        if (!context.Sps.Range.PersistentRice)
        {
            return;
        }

        ref var stat = ref context.StatCoeff[state.StatType];
        if (remaining >= (RiceGrowFactor << (stat / StatDivisor)))
        {
            stat++;
        }
        else if (remaining << 1 < (1 << (stat / StatDivisor)) && stat > 0)
        {
            stat--;
        }
    }

    /// <summary>Works out the Rice parameter of the next remaining magnitude (9-24 and 9-25).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="rice">The parameter just used.</param>
    /// <param name="absolute">The level just read.</param>
    /// <returns>The next parameter.</returns>
    private static int NextRice(H265SliceContext context, int rice, int absolute)
    {
        var next = rice + (absolute > RiceGrowFactor * (1 << rice) ? 1 : 0);
        return context.Sps.Range.PersistentRice ? next : Math.Min(next, MaxRice);
    }

    /// <summary>Reads coeff_abs_level_remaining: a truncated Rice prefix with an Exp-Golomb suffix (9.3.3.11).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="rice">The Rice parameter.</param>
    /// <returns>The magnitude.</returns>
    private static int ReadRemaining(ref CabacEngine engine, int rice)
    {
        var prefix = 0;
        while (prefix < RemainingPrefixLimit && engine.DecodeBypass() == 1)
        {
            prefix++;
        }

        return prefix < RemainingPrefixLimit
            ? (prefix << rice) + H265Bins.ReadBypass(ref engine, rice)
            : (RemainingPrefixLimit << rice) + H265Bins.ReadExpGolombBypass(ref engine, rice + 1);
    }
}
