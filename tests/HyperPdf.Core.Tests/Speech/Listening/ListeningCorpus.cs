// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>The listening corpus, <c>tests/listening/corpus.json</c>.</summary>
/// <param name="Comment">What the file is.</param>
/// <param name="Cases">The phrases checked for how they are said.</param>
/// <param name="Passages">The passages read by the real voice.</param>
[DebuggerDisplay("ListeningCorpus: {Cases.Count} cases, {Passages.Count} passages")]
public sealed record ListeningCorpus(string Comment, IReadOnlyList<ListeningCase> Cases, IReadOnlyList<ListeningPassage> Passages)
{
    /// <summary>Gets the corpus copied next to the tests.</summary>
    public static ListeningCorpus Instance { get; } = Load();

    /// <summary>Gets the cases, for TUnit.</summary>
    /// <returns>The cases.</returns>
    public static IEnumerable<Func<ListeningCase>> AllCases()
    {
        foreach (var item in Instance.Cases)
        {
            yield return () => item;
        }
    }

    /// <summary>Gets the passages, for TUnit.</summary>
    /// <returns>The passages.</returns>
    public static IEnumerable<Func<ListeningPassage>> AllPassages()
    {
        foreach (var item in Instance.Passages)
        {
            yield return () => item;
        }
    }

    /// <summary>Reads the corpus.</summary>
    /// <returns>The corpus.</returns>
    private static ListeningCorpus Load()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "listening-corpus.json"));
        return JsonSerializer.Deserialize(stream, ListeningJsonContext.Default.ListeningCorpus)!;
    }
}
