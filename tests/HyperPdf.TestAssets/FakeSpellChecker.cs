// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Spelling;

namespace HyperPdf.TestAssets;

/// <summary>A test spell checker that knows a short list of words and the corrections for a few misspellings.</summary>
[DebuggerDisplay("FakeSpellChecker")]
public sealed class FakeSpellChecker : ISpellChecker
{
    /// <summary>The words spelled correctly.</summary>
    private static readonly HashSet<string> Words = new(
        ["the", "quick", "brown", "fox", "please", "receive", "signed", "form", "name", "address", "don't", "PDF", "hello"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the shared instance.</summary>
    public static FakeSpellChecker Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public bool IsCorrect(ReadOnlySpan<char> word) => Words.GetAlternateLookup<ReadOnlySpan<char>>().Contains(word);

    /// <inheritdoc/>
    public void Suggest(string word, List<string> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        switch (word.ToUpperInvariant())
        {
            case "RECIEVE":
            {
                suggestions.AddRange(["receive", "relieve"]);
                break;
            }

            case "TEH":
            {
                suggestions.Add("the");
                break;
            }

            case "ADRESS":
            {
                suggestions.Add("address");
                break;
            }
        }
    }
}
