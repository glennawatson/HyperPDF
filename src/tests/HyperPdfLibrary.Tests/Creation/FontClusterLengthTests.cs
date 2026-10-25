// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests that the shortcut for ASCII text in <see cref="FontFallbackResolver.GetNextClusterLength"/> agrees with the platform's grapheme segmentation.</summary>
public sealed class FontClusterLengthTests
{
    /// <summary>The first character that is not ASCII.</summary>
    private const int NonAscii = 0x80;

    /// <summary>The first combining mark.</summary>
    private const int FirstExtender = 0x300;

    /// <summary>The carriage return, which joins a following line feed.</summary>
    private const int CarriageReturn = 13;

    /// <summary>Verifies every ASCII character followed by any character below the first combining mark is a cluster of its own, as the segmenter says, except a CR before an LF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsciiBeforeLowCharactersIsOneCluster()
    {
        var mismatches = 0;
        for (var first = 0; first < NonAscii; first++)
        {
            for (var second = 0; second < FirstExtender; second++)
            {
                mismatches += Differs([(char)first, (char)second]) ? 1 : 0;
            }

            mismatches += Differs([(char)first]) ? 1 : 0;
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Verifies clusters the shortcut must not take are still found whole.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The length of the first cluster.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("áb", 2)]
    [Arguments("\r\nb", 2)]
    [Arguments("\U0001F1FA\U0001F1F8b", 4)]
    [Arguments("ab", 1)]
    public async Task ClustersWithExtendersAreKeptWhole(string text, int length) =>
        await Assert.That(FontFallbackResolver.GetNextClusterLength(text)).IsEqualTo(length);

    /// <summary>Checks one text against the segmenter.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether the cluster lengths differ.</returns>
    private static bool Differs(ReadOnlySpan<char> text) =>
        text[0] != CarriageReturn && FontFallbackResolver.GetNextClusterLength(text) != StringInfo.GetNextTextElementLength(text);
}
