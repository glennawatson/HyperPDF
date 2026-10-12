// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Reads the veraPDF results recorded for the corpus by <c>scripts/RecordVeraPdfCorpusResults.cs</c>
/// (<c>Data/verapdf-corpus-results.tsv</c>, embedded in the test assembly).
/// </summary>
internal static class VeraPdfCorpusResults
{
    /// <summary>The manifest name of the recorded results.</summary>
    private const string ResourceName = "HyperPdfLibrary.Tests.VeraPdfCorpusResults.tsv";

    /// <summary>The tab-separated columns of a result line.</summary>
    private const int Columns = 5;

    /// <summary>The column holding the job status.</summary>
    private const int StatusColumn = 3;

    /// <summary>The column holding the failed rules.</summary>
    private const int FailedColumn = 4;

    /// <summary>The job status of a validation veraPDF finished normally.</summary>
    private const string NormalStatus = "normal";

    /// <summary>Gets the levels by veraPDF flavour.</summary>
    private static Dictionary<string, PdfALevel> Levels => new(StringComparer.Ordinal)
    {
        ["1a"] = PdfALevel.PdfA1A,
        ["1b"] = PdfALevel.PdfA1B,
        ["2a"] = PdfALevel.PdfA2A,
        ["2b"] = PdfALevel.PdfA2B,
        ["2u"] = PdfALevel.PdfA2U,
        ["3b"] = PdfALevel.PdfA3B,
        ["4"] = PdfALevel.PdfA4,
        ["4e"] = PdfALevel.PdfA4E,
        ["4f"] = PdfALevel.PdfA4F,
    };

    /// <summary>Reads the recorded results.</summary>
    /// <returns>One result per corpus file.</returns>
    /// <exception cref="InvalidDataException">The record is missing or malformed.</exception>
    internal static List<VeraPdfCorpusResult> Read()
    {
        using var stream = typeof(VeraPdfCorpusResults).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The recorded veraPDF corpus results are missing from the test assembly.");
        using var reader = new StreamReader(stream);
        var levels = Levels;
        var results = new List<VeraPdfCorpusResult>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length != Columns || !levels.TryGetValue(columns[1], out var level))
            {
                throw new InvalidDataException($"Malformed recorded veraPDF result: {line}");
            }

            var failed = new HashSet<string>(columns[FailedColumn].Split(';', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            results.Add(new(columns[0], level, columns[StatusColumn] == NormalStatus, failed));
        }

        return results;
    }
}
