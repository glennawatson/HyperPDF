// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.English;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Tests speech normalization and reuse of its buffers.</summary>
public sealed class TextNormalizerTests
{
    /// <summary>Candidate scanning keeps multiple matches, word boundaries and rejected dates intact.</summary>
    /// <param name="text">The written text.</param>
    /// <param name="expected">The spoken text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Meet on 12 March and 3 May.", "Meet on March twelfth and May third.")]
    [Arguments("At 9am and 12pm.", "At 9 A M and 12 P M")]
    [Arguments("A12 March and 3pmB.", "A12 March and 3pmB.")]
    [Arguments("é12 March and _9am", "é12 March and _9am")]
    [Arguments("Meet 0 March, 03 March and 32 March.", "Meet 0 March, 03 March and 32 March.")]
    [Arguments("Meet 12 March, then at 9am.", "Meet March twelfth, then at 9 A M")]
    [Arguments("12 March 13 April", "March twelfth April thirteenth")]
    [Arguments("(9am), 11PM", "(9 A M), 11 P M")]
    public async Task PreservesCandidateMatchBoundaries(string text, string expected)
    {
        var actual = TextNormalizer.Normalize(text, false, new()).ToString();

        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Reusing both buffers does not retain words from a longer preceding sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusesBuffersAcrossSentences()
    {
        var buffers = new NormalizerBuffers();
        var first = TextNormalizer.Normalize("Dr. Smith paid $1.50 [4].", false, buffers).ToString();
        var second = TextNormalizer.Normalize("Hello.", false, buffers).ToString();
        var empty = TextNormalizer.Normalize(string.Empty, false, buffers).ToString();

        await Assert.That(first).IsEqualTo("Doctor Smith paid 1 dollar and 50 cents.");
        await Assert.That(second).IsEqualTo("Hello.");
        await Assert.That(empty).IsEqualTo(string.Empty);
    }
}
