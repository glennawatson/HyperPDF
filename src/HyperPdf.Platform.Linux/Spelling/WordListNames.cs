// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>The file names Linux word list packages use for each language, the reader's own list first.</summary>
public static class WordListNames
{
    /// <summary>The US English list.</summary>
    private const string American = "american-english";

    /// <summary>The British English list.</summary>
    private const string British = "british-english";

    /// <summary>The list most distributions link to the default language's words.</summary>
    private const string Words = "words";

    /// <summary>Gets the names to try for a language, most fitting first.</summary>
    /// <param name="culture">The language.</param>
    /// <returns>The file names.</returns>
    public static IReadOnlyList<string> For(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        List<string> names = [$"{culture.Name}.txt", $"{culture.TwoLetterISOLanguageName}.txt"];
        names.AddRange(culture.Name switch
        {
            "en-GB" or "en-IE" or "en-NZ" or "en-ZA" or "en-IN" => [British, "british-english-large", American, Words],
            "en-CA" => ["canadian-english", British, American, Words],
            "en-AU" => ["australian-english", British, American, Words],
            _ => [],
        });
        names.AddRange(culture.TwoLetterISOLanguageName switch
        {
            "en" => [American, "american-english-large", British, Words],
            "de" => ["ngerman", "ogerman", "swiss"],
            "fr" => ["french"],
            "es" => ["spanish"],
            "it" => ["italian"],
            "pt" => ["portuguese", "brazilian"],
            "nl" => ["dutch"],
            "sv" => ["swedish"],
            "da" => ["danish"],
            "nb" or "nn" or "no" => ["bokmaal", "nynorsk", "norwegian"],
            "fi" => ["finnish"],
            "pl" => ["polish"],
            "ca" => ["catalan"],
            _ => [],
        });
        return names;
    }
}
