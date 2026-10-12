// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the PDF/A rule table: for each rule, the clause and test number each level gives it, and its description. The
/// table is the embedded <c>Data/PdfARules.tsv</c>, read once on first use.
/// </summary>
internal static class PdfARuleCatalog
{
    /// <summary>The manifest name of the rule table.</summary>
    private const string ResourceName = "HyperPdfLibrary.Conformance.PdfARules.tsv";

    /// <summary>The number of tab-separated columns in a table line.</summary>
    private const int Columns = 3;

    /// <summary>The number of colon-separated parts in a level reference.</summary>
    private const int ReferenceParts = 3;

    /// <summary>The level codes the table uses, in <see cref="PdfALevel"/> order.</summary>
    private static readonly string[] LevelCodes = ["1B", "1A", "2B", "2U", "2A", "3B", "3U", "3A", "4", "4E", "4F"];

    /// <summary>The parsed table.</summary>
    private static readonly PdfARuleTable Table = Load();

    /// <summary>Gets the rules a level applies, in table order.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The rules with the clause and test the level numbers them by.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfARuleReference[] For(PdfALevel level) => Table.ByLevel[(int)level];

    /// <summary>Gets a rule's description, quoted from the veraPDF profiles.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Describe(PdfARule rule) => Table.Descriptions[(int)rule];

    /// <summary>Reads the embedded table.</summary>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The table is missing or malformed.</exception>
    private static PdfARuleTable Load()
    {
        using var stream = typeof(PdfARuleCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The PDF/A rule table is missing from the library.");
        using var reader = new StreamReader(stream);
        var descriptions = new List<string>();
        var byLevel = new List<PdfARuleReference>[LevelCodes.Length];
        for (var i = 0; i < byLevel.Length; i++)
        {
            byLevel[i] = [];
        }

        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var columns = line.Split('\t', Columns);
            if (columns.Length != Columns || !int.TryParse(columns[0], out var number) || number != descriptions.Count)
            {
                throw new InvalidDataException("The PDF/A rule table is malformed.");
            }

            descriptions.Add(columns[2]);
            AddReferences((PdfARule)number, columns[1], byLevel);
        }

        var levels = new PdfARuleReference[byLevel.Length][];
        for (var i = 0; i < levels.Length; i++)
        {
            levels[i] = [.. byLevel[i]];
        }

        return new([.. descriptions], levels);
    }

    /// <summary>Adds a rule to each level its reference column names.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="column">Space-separated <c>level:clause:test</c> references.</param>
    /// <param name="byLevel">The rules per level.</param>
    /// <exception cref="InvalidDataException">A reference is malformed.</exception>
    private static void AddReferences(PdfARule rule, string column, List<PdfARuleReference>[] byLevel)
    {
        foreach (var reference in column.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = reference.Split(':');
            var level = parts.Length == ReferenceParts ? Array.IndexOf(LevelCodes, parts[0]) : -1;
            if (level < 0)
            {
                throw new InvalidDataException("The PDF/A rule table has a malformed reference.");
            }

            byLevel[level].Add(new(rule, parts[1], parts[2]));
        }
    }
}
