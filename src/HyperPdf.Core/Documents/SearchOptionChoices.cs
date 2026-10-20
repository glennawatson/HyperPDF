// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Builds <see cref="SearchOptions"/> from the choices a person makes in a find bar or search window.</summary>
public static class SearchOptionChoices
{
    /// <summary>Gets the options the choices stand for.</summary>
    /// <param name="matchCase">Whether letter case must match.</param>
    /// <param name="wholeWord">Whether only whole words match.</param>
    /// <param name="ignoreDiacritics">Whether accents are ignored.</param>
    /// <param name="foldCompatibility">Whether look-alike characters match.</param>
    /// <returns>The options.</returns>
    public static SearchOptions Of(bool matchCase, bool wholeWord, bool ignoreDiacritics, bool foldCompatibility) =>
        (matchCase ? SearchOptions.MatchCase : SearchOptions.None)
        | (wholeWord ? SearchOptions.WholeWord : SearchOptions.None)
        | (ignoreDiacritics ? SearchOptions.IgnoreDiacritics : SearchOptions.None)
        | (foldCompatibility ? SearchOptions.FoldCompatibility : SearchOptions.None);
}
