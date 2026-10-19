// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Reads the rules of veraPDF's PDF/UA, tagged PDF, Well-Tagged PDF and WCAG profiles. The table is the embedded
/// <c>Data/StandardsRules.tsv</c>, which <c>scripts/StandardsRuleTableGenerator.cs</c> writes from the profile files; a rule that several
/// profiles share is stored once with a bit for each profile that holds it.
/// </summary>
internal static class StandardsRuleCatalog
{
    /// <summary>The manifest name of the rule table.</summary>
    private const string ResourceName = "HyperPdfLibrary.Accessibility.StandardsRules.tsv";

    /// <summary>The number of tab-separated columns in a table line.</summary>
    private const int Columns = 6;

    /// <summary>The number of profiles, which is the number of bits a line can set.</summary>
    private const int ProfileCount = 13;

    /// <summary>The tags that mark a rule only a person can judge.</summary>
    private static readonly string[] HumanTags = ["human", "contrast"];

    /// <summary>The rules of each profile, indexed by <see cref="PdfStandardsProfile"/>; filled once and never changed.</summary>
    private static readonly PdfStandardsRule[][] ByProfile = Load();

    /// <summary>Gets the rules of a profile, in table order.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The rules; the array is shared, so callers must not change it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not one of the defined profiles.</exception>
    internal static PdfStandardsRule[] For(PdfStandardsProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)profile);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((int)profile, ProfileCount);
        return ByProfile[(int)profile];
    }

    /// <summary>Reads the embedded table.</summary>
    /// <returns>The rules of each profile.</returns>
    /// <exception cref="InvalidDataException">The table is missing or malformed.</exception>
    private static PdfStandardsRule[][] Load()
    {
        using var stream = typeof(StandardsRuleCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The standards rule table is missing from the library.");
        using var reader = new StreamReader(stream);
        var lists = new List<PdfStandardsRule>[ProfileCount];
        for (var index = 0; index < lists.Length; index++)
        {
            lists[index] = [];
        }

        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var columns = line.Split('\t', Columns);
            if (columns.Length != Columns || !int.TryParse(columns[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var profiles))
            {
                throw new InvalidDataException("The standards rule table is malformed.");
            }

            var rule = new PdfStandardsRule(columns[1], columns[2], columns[3], columns[5], NeedsHumanReview(columns[4]));
            for (var index = 0; index < lists.Length; index++)
            {
                if ((profiles & (1 << index)) != 0)
                {
                    lists[index].Add(rule);
                }
            }
        }

        var all = new PdfStandardsRule[ProfileCount][];
        for (var index = 0; index < all.Length; index++)
        {
            all[index] = [.. lists[index]];
        }

        return all;
    }

    /// <summary>Determines whether a rule's tags mark it for a person to judge.</summary>
    /// <param name="tags">The comma-separated tags.</param>
    /// <returns><see langword="true"/> when one of them does.</returns>
    private static bool NeedsHumanReview(string tags)
    {
        foreach (var tag in HumanTags)
        {
            if (tags.Contains(tag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
