// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// The CMU pronouncing dictionary as MeloTTS reads it (<c>melo-en-lexicon.txt</c>): one word per line, a tab, and its
/// ARPAbet phones. Each word's phones are stored once as packed symbol ids and tones.
/// </summary>
[DebuggerDisplay("MeloLexicon: {Count} words")]
internal sealed class MeloLexicon
{
    /// <summary>About how many characters of the file each phone takes, for the first allocation.</summary>
    private const int BytesPerPhone = 3;

    /// <summary>The longest word read.</summary>
    private const int MaxWordLength = 256;

    /// <summary>Where each word's phones start and how many there are, by lower-case word.</summary>
    private readonly Dictionary<string, (int Start, int Count)> _words;

    /// <summary>The words, looked up by span.</summary>
    private readonly Dictionary<string, (int Start, int Count)>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    /// <summary>Every word's packed phones, end to end.</summary>
    private readonly int[] _phones;

    /// <summary>Initializes a new instance of the <see cref="MeloLexicon"/> class.</summary>
    /// <param name="words">The words.</param>
    /// <param name="phones">The phones.</param>
    private MeloLexicon(Dictionary<string, (int Start, int Count)> words, int[] phones)
    {
        _words = words;
        _lookup = words.GetAlternateLookup<ReadOnlySpan<char>>();
        _phones = phones;
    }

    /// <summary>Gets the number of words.</summary>
    internal int Count => _words.Count;

    /// <summary>Reads the dictionary.</summary>
    /// <param name="text">The dictionary's text.</param>
    /// <param name="symbols">The symbol table the phones are packed with.</param>
    /// <returns>The dictionary.</returns>
    internal static MeloLexicon Load(string text, MeloSymbols symbols)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(symbols);
        var words = new Dictionary<string, (int Start, int Count)>(StringComparer.Ordinal);
        var phones = new List<int>(text.Length / BytesPerPhone);
        Span<char> lower = stackalloc char[MaxWordLength];
        foreach (var line in text.AsSpan().EnumerateLines())
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0 || tab > lower.Length)
            {
                continue;
            }

            var start = phones.Count;
            foreach (var range in line[(tab + 1)..].Split(' '))
            {
                var phone = line[(tab + 1)..][range];
                if (!phone.IsEmpty)
                {
                    phones.Add(symbols.FromDictionary(phone));
                }
            }

            var length = line[..tab].ToLowerInvariant(lower);
            _ = words.TryAdd(lower[..length].ToString(), (start, phones.Count - start));
        }

        return new(words, [.. phones]);
    }

    /// <summary>Looks a word up.</summary>
    /// <param name="word">The lower-case word.</param>
    /// <param name="phones">Its packed phones.</param>
    /// <returns><see langword="true"/> when the dictionary holds the word.</returns>
    internal bool TryGet(scoped ReadOnlySpan<char> word, out ReadOnlySpan<int> phones)
    {
        if (_lookup.TryGetValue(word, out var entry))
        {
            phones = _phones.AsSpan(entry.Start, entry.Count);
            return true;
        }

        phones = default;
        return false;
    }
}
