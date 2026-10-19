// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Tests.Search;

/// <summary>Checks how find bar choices become search options.</summary>
public sealed class SearchOptionChoicesTests
{
    /// <summary>Verifies that each choice sets exactly its own option.</summary>
    /// <param name="matchCase">Whether letter case must match.</param>
    /// <param name="wholeWord">Whether only whole words match.</param>
    /// <param name="ignoreDiacritics">Whether accents are ignored.</param>
    /// <param name="foldCompatibility">Whether look-alike characters match.</param>
    /// <param name="expected">The options the choices stand for.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false, false, false, SearchOptions.None)]
    [Arguments(true, false, false, false, SearchOptions.MatchCase)]
    [Arguments(false, true, false, false, SearchOptions.WholeWord)]
    [Arguments(false, false, true, false, SearchOptions.IgnoreDiacritics)]
    [Arguments(false, false, false, true, SearchOptions.FoldCompatibility)]
    [Arguments(true, true, true, true, SearchOptions.MatchCase | SearchOptions.WholeWord | SearchOptions.IgnoreDiacritics | SearchOptions.FoldCompatibility)]
    public async Task Of_SetsOnlyTheChosenOptions(bool matchCase, bool wholeWord, bool ignoreDiacritics, bool foldCompatibility, SearchOptions expected) =>
        await Assert.That(SearchOptionChoices.Of(matchCase, wholeWord, ignoreDiacritics, foldCompatibility)).IsEqualTo(expected);
}
