// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Reads the veraPDF results recorded for veraPDF's PDF/UA test files by <c>src/tools/RecordVeraPdfResults.cs</c> with its
/// <c>ua</c> option (<c>Data/verapdf-ua-results.tsv</c>, embedded in the test assembly).
/// </summary>
internal static class VeraPdfUaRecordedResults
{
    /// <summary>The manifest name of the recorded results.</summary>
    private const string ResourceName = "HyperPdfLibrary.Tests.VeraPdfUaRecordedResults.tsv";

    /// <summary>Part 2 of ISO 14289.</summary>
    private const int PartTwo = 2;

    /// <summary>The tab-separated columns of a result line.</summary>
    private const int Columns = 5;

    /// <summary>The column holding the job status.</summary>
    private const int StatusColumn = 3;

    /// <summary>The column holding the failed rules.</summary>
    private const int FailedColumn = 4;

    /// <summary>The job status of a validation veraPDF finished normally.</summary>
    private const string NormalStatus = "normal";

    /// <summary>Gets the parts of ISO 14289 by veraPDF flavour.</summary>
    private static Dictionary<string, int> Parts => new(StringComparer.Ordinal) { ["ua1"] = 1, ["ua2"] = PartTwo };

    /// <summary>Reads the recorded results.</summary>
    /// <returns>One result per test file.</returns>
    /// <exception cref="InvalidDataException">The record is missing or malformed.</exception>
    internal static List<VeraPdfUaRecordedResult> Read()
    {
        using var stream = typeof(VeraPdfUaRecordedResults).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The recorded veraPDF PDF/UA results are missing from the test assembly.");
        using var reader = new StreamReader(stream);
        var parts = Parts;
        var results = new List<VeraPdfUaRecordedResult>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length != Columns || !parts.TryGetValue(columns[1], out var part))
            {
                throw new InvalidDataException($"Malformed recorded veraPDF result: {line}");
            }

            var failed = new HashSet<string>(columns[FailedColumn].Split(';', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            results.Add(new(columns[0], part, columns[StatusColumn] == NormalStatus, failed));
        }

        return results;
    }
}
