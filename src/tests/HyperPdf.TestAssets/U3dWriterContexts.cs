// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.TestAssets;

/// <summary>
/// The adaptive frequency counts of one U3D object while it is written, kept apart from the reader's so that a test of the reader
/// checks it against the encoder of ECMA-363 clause 10.4 and not against its own code. Symbol 0 is the escape symbol.
/// </summary>
public sealed class U3dWriterContexts
{
    /// <summary>The total at which the counts halve.</summary>
    private const uint HalvingTotal = 0x1FFF;

    /// <summary>The counts of each adaptive context; index 0 is the escape symbol.</summary>
    private readonly Dictionary<uint, List<uint>> _counts = [];

    /// <summary>Gets the adaptive context of a shading list index.</summary>
    public static uint Shading { get; } = 1;

    /// <summary>Gets the adaptive context of the count of unique knots in the U direction.</summary>
    public static uint KnotCountU { get; } = 2;

    /// <summary>Gets the adaptive context of a knot's multiplicity in the U direction.</summary>
    public static uint KnotMultiplicityU { get; } = 3;

    /// <summary>Gets the adaptive context of the count of unique knots in the V direction.</summary>
    public static uint KnotCountV { get; } = 4;

    /// <summary>Gets the adaptive context of a knot's multiplicity in the V direction.</summary>
    public static uint KnotMultiplicityV { get; } = 5;

    /// <summary>Gets the first value of a static context: this plus the range.</summary>
    public static uint StaticBase { get; } = 0x400;

    /// <summary>Gets the total of all counts of a context, escape included.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <returns>The total.</returns>
    public uint Total(uint context) => (uint)Counts(context).Sum(static count => (long)count);

    /// <summary>Gets the count of one symbol.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The count; zero for a symbol never seen.</returns>
    public uint CountOf(uint context, uint symbol)
    {
        var counts = Counts(context);
        return symbol < counts.Count ? counts[(int)symbol] : 0;
    }

    /// <summary>Gets the sum of the counts of every symbol below one.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The sum.</returns>
    public uint CountBelow(uint context, uint symbol)
    {
        var counts = Counts(context);
        uint sum = 0;
        for (var i = 0; i < counts.Count && i < symbol; i++)
        {
            sum += counts[i];
        }

        return sum;
    }

    /// <summary>Counts a symbol, halving every count first when the total has reached its limit.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <param name="symbol">The symbol, with the escape symbol as zero.</param>
    public void Add(uint context, uint symbol)
    {
        if (symbol >= ushort.MaxValue)
        {
            return;
        }

        var counts = Counts(context);
        while (counts.Count <= symbol)
        {
            counts.Add(0);
        }

        if (Total(context) >= HalvingTotal)
        {
            for (var i = 0; i < counts.Count; i++)
            {
                counts[i] >>= 1;
            }

            counts[0]++;
        }

        counts[(int)symbol]++;
    }

    /// <summary>Gets the counts of a context, starting it with an escape count of one.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <returns>The counts.</returns>
    private List<uint> Counts(uint context)
    {
        ref var counts = ref CollectionsMarshal.GetValueRefOrAddDefault(_counts, context, out _);
        counts ??= [1];
        return counts;
    }
}
