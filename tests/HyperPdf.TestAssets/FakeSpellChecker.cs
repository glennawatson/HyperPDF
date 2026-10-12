// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using HyperPdf.Core.Spelling;

namespace HyperPdf.TestAssets;

/// <summary>A test spell checker that knows a short list of words and the corrections for a few misspellings.</summary>
[DebuggerDisplay("FakeSpellChecker")]
public sealed class FakeSpellChecker : ISpellChecker
{
    /// <summary>The words spelled correctly.</summary>
    private static readonly FrozenSet<string> Words = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ["the", "quick", "brown", "fox", "please", "receive", "signed", "form", "name", "address", "don't", "PDF", "hello"]);

    /// <summary>Gets the shared instance.</summary>
    public static FakeSpellChecker Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public bool IsCorrect(ReadOnlySpan<char> word) => Words.GetAlternateLookup<ReadOnlySpan<char>>().Contains(word);

    /// <inheritdoc/>
    public IReadOnlyList<string> Suggest(string word) => word.ToUpperInvariant() switch
    {
        "RECIEVE" => ["receive", "relieve"],
        "TEH" => ["the"],
        "ADRESS" => ["address"],
        _ => [],
    };
}
