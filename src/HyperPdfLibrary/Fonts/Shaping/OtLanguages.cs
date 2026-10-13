// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Maps BCP 47 language tags to the OpenType language system tags a font may have (OpenType, "Language system tags").</summary>
internal static class OtLanguages
{
    /// <summary>The characters in a primary language subtag that is looked up.</summary>
    private const int PrimaryLength = 2;

    /// <summary>Gets the OpenType language system tag for a language.</summary>
    /// <param name="language">The BCP 47 language tag, such as <c>ar</c> or <c>zh-Hant</c>, or <see langword="null"/>.</param>
    /// <returns>The tag; zero when the language is not known, so the script's default language system is used.</returns>
    internal static uint TagFor(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return 0;
        }

        var span = language.AsSpan();
        var dash = span.IndexOfAny('-', '_');
        var primary = dash < 0 ? span : span[..dash];
        var rest = dash < 0 ? default : span[(dash + 1)..];
        if (primary.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return OtTag.Of(ChineseTag(rest));
        }

        return primary.Length == PrimaryLength ? Lookup(primary) : 0;
    }

    /// <summary>Picks the tag of a Chinese language from its script or region.</summary>
    /// <param name="rest">The subtags after <c>zh</c>.</param>
    /// <returns>The four-character tag.</returns>
    private static string ChineseTag(ReadOnlySpan<char> rest)
    {
        if (rest.Contains("hant", StringComparison.OrdinalIgnoreCase) || rest.Contains("tw", StringComparison.OrdinalIgnoreCase))
        {
            return "ZHT ";
        }

        return rest.Contains("hk", StringComparison.OrdinalIgnoreCase) ? "ZHH " : "ZHS ";
    }

    /// <summary>Looks up a two-letter language.</summary>
    /// <param name="primary">The primary language subtag.</param>
    /// <returns>The tag; zero when the language is not known.</returns>
    private static uint Lookup(ReadOnlySpan<char> primary)
    {
        Span<char> lower = stackalloc char[PrimaryLength];
        _ = primary.ToLowerInvariant(lower);
        var tag = lower switch
        {
            "ar" => "ARA ",
            "fa" => "FAR ",
            "ur" => "URD ",
            "ps" => "PAS ",
            "sd" => "SND ",
            "he" => "IWR ",
            "yi" => "JII ",
            "hi" => "HIN ",
            "mr" => "MAR ",
            "ne" => "NEP ",
            "bn" => "BEN ",
            "pa" => "PAN ",
            "gu" => "GUJ ",
            "or" => "ORI ",
            "ta" => "TAM ",
            "te" => "TEL ",
            "kn" => "KAN ",
            "ml" => "MAL ",
            "tr" => "TRK ",
            "ro" => "ROM ",
            "ja" => "JAN ",
            "ko" => "KOR ",
            "vi" => "VIT ",
            "th" => "THA ",
            "nl" => "NLD ",
            "pl" => "PLK ",
            "az" => "AZE ",
            "lt" => "LTH ",
            "lv" => "LVI ",
            "sr" => "SRB ",
            "bg" => "BGR ",
            "ru" => "RUS ",
            "uk" => "UKR ",
            "el" => "ELL ",
            "fr" => "FRA ",
            "de" => "DEU ",
            "es" => "ESP ",
            "it" => "ITA ",
            "pt" => "PTG ",
            "sv" => "SVE ",
            "fi" => "FIN ",
            "da" => "DAN ",
            _ => null,
        };
        return tag is null ? 0 : OtTag.Of(tag);
    }
}
