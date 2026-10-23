// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// The standard Huffman tables B.1 to B.15 of the JBIG2 standard (annex B.5), transcribed line by line in the order the
/// standard lists them, which is the order of its prefix code assignment (B.3). The decoder's tables are checked against these.
/// </summary>
internal static class Jbig2SpecTables
{
    /// <summary>The range length of the lower and upper range lines.</summary>
    private const int RangeBits = 32;

    /// <summary>The lines of each standard table, B.1 first.</summary>
    private static readonly Jbig2SpecLine[][] Tables =
    [
        [N(0, 1, 4), N(16, 2, 8), N(272, 3, 16), U(65_808, 3)],
        [N(0, 1, 0), N(1, 2, 0), N(2, 3, 0), N(3, 4, 3), N(11, 5, 6), U(75, 6), O(6)],
        [N(-256, 8, 8), N(0, 1, 0), N(1, 2, 0), N(2, 3, 0), N(3, 4, 3), N(11, 5, 6), L(-257, 8), U(75, 7), O(6)],
        [N(1, 1, 0), N(2, 2, 0), N(3, 3, 0), N(4, 4, 3), N(12, 5, 6), U(76, 5)],
        [N(-255, 7, 8), N(1, 1, 0), N(2, 2, 0), N(3, 3, 0), N(4, 4, 3), N(12, 5, 6), L(-256, 7), U(76, 6)],
        [
            N(-2048, 5, 10), N(-1024, 4, 9), N(-512, 4, 8), N(-256, 4, 7), N(-128, 5, 6), N(-64, 5, 5), N(-32, 4, 5),
            N(0, 2, 7), N(128, 3, 7), N(256, 3, 8), N(512, 4, 9), N(1024, 4, 10), L(-2049, 6), U(2048, 6),
        ],
        [
            N(-1024, 4, 9), N(-512, 3, 8), N(-256, 4, 7), N(-128, 5, 6), N(-64, 5, 5), N(-32, 4, 5), N(0, 4, 5),
            N(32, 5, 5), N(64, 5, 6), N(128, 4, 7), N(256, 3, 8), N(512, 3, 9), N(1024, 3, 10), L(-1025, 5), U(2048, 5),
        ],
        [
            N(-15, 8, 3), N(-7, 9, 1), N(-5, 8, 1), N(-3, 9, 0), N(-2, 7, 0), N(-1, 4, 0), N(0, 2, 1), N(2, 5, 0),
            N(3, 6, 0), N(4, 3, 4), N(20, 6, 1), N(22, 4, 4), N(38, 4, 5), N(70, 5, 6), N(134, 5, 7), N(262, 6, 7),
            N(390, 7, 8), N(646, 6, 10), L(-16, 9), U(1670, 9), O(2),
        ],
        [
            N(-31, 8, 4), N(-15, 9, 2), N(-11, 8, 2), N(-7, 9, 1), N(-5, 7, 1), N(-3, 4, 1), N(-1, 3, 1), N(1, 3, 1),
            N(3, 5, 1), N(5, 6, 1), N(7, 3, 5), N(39, 6, 2), N(43, 4, 5), N(75, 4, 6), N(139, 5, 7), N(267, 5, 8),
            N(523, 6, 8), N(779, 7, 9), N(1291, 6, 11), L(-32, 9), U(3339, 9), O(2),
        ],
        [
            N(-21, 7, 4), N(-5, 8, 0), N(-4, 7, 0), N(-3, 5, 0), N(-2, 2, 2), N(2, 5, 0), N(3, 6, 0), N(4, 7, 0),
            N(5, 8, 0), N(6, 2, 6), N(70, 5, 5), N(102, 6, 5), N(134, 6, 6), N(198, 6, 7), N(326, 6, 8), N(582, 6, 9),
            N(1094, 6, 10), N(2118, 7, 11), L(-22, 8), U(4166, 8), O(2),
        ],
        [
            N(1, 1, 0), N(2, 2, 1), N(4, 4, 0), N(5, 4, 1), N(7, 5, 1), N(9, 5, 2), N(13, 6, 2), N(17, 7, 2),
            N(21, 7, 3), N(29, 7, 4), N(45, 7, 5), N(77, 7, 6), U(141, 7),
        ],
        [
            N(1, 1, 0), N(2, 2, 0), N(3, 3, 1), N(5, 5, 0), N(6, 5, 1), N(8, 6, 1), N(10, 7, 0), N(11, 7, 1),
            N(13, 7, 2), N(17, 7, 3), N(25, 7, 4), N(41, 8, 5), U(73, 8),
        ],
        [
            N(1, 1, 0), N(2, 3, 0), N(3, 4, 0), N(4, 5, 0), N(5, 4, 1), N(7, 3, 3), N(15, 6, 1), N(17, 6, 2),
            N(21, 6, 3), N(29, 6, 4), N(45, 6, 5), N(77, 7, 6), U(141, 7),
        ],
        [N(-2, 3, 0), N(-1, 3, 0), N(0, 1, 0), N(1, 3, 0), N(2, 3, 0)],
        [
            N(-24, 7, 4), N(-8, 6, 2), N(-4, 5, 1), N(-2, 4, 0), N(-1, 3, 0), N(0, 1, 0), N(1, 3, 0), N(2, 4, 0),
            N(3, 5, 1), N(5, 6, 2), N(9, 7, 4), L(-25, 7), U(25, 7),
        ],
    ];

    /// <summary>Gets the number of standard tables.</summary>
    internal static int Count => Tables.Length;

    /// <summary>Gets the lines of a standard table.</summary>
    /// <param name="number">The table number, 1 for B.1.</param>
    /// <returns>The lines, in the standard's order.</returns>
    internal static ReadOnlySpan<Jbig2SpecLine> Lines(int number) => Tables[number - 1];

    /// <summary>Makes an ordinary line.</summary>
    /// <param name="low">The first value.</param>
    /// <param name="prefix">The prefix length.</param>
    /// <param name="range">The range length.</param>
    /// <returns>The line.</returns>
    private static Jbig2SpecLine N(int low, int prefix, int range) => new(low, prefix, range, Jbig2SpecLineKind.Normal);

    /// <summary>Makes a lower range line, which has a 32-bit range.</summary>
    /// <param name="low">The value the range counts down from.</param>
    /// <param name="prefix">The prefix length.</param>
    /// <returns>The line.</returns>
    private static Jbig2SpecLine L(int low, int prefix) => new(low, prefix, RangeBits, Jbig2SpecLineKind.Lower);

    /// <summary>Makes an upper range line, which has a 32-bit range.</summary>
    /// <param name="low">The first value.</param>
    /// <param name="prefix">The prefix length.</param>
    /// <returns>The line.</returns>
    private static Jbig2SpecLine U(int low, int prefix) => new(low, prefix, RangeBits, Jbig2SpecLineKind.Upper);

    /// <summary>Makes an out-of-band line.</summary>
    /// <param name="prefix">The prefix length.</param>
    /// <returns>The line.</returns>
    private static Jbig2SpecLine O(int prefix) => new(0, prefix, 0, Jbig2SpecLineKind.OutOfBand);
}
