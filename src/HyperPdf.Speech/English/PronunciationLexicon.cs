// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace HyperPdf.Speech.English;

/// <summary>
/// The misaki pronunciation dictionaries (Apache-2.0): words to the phonemes Kokoro was trained on. The hand checked
/// "gold" entries win over the larger "silver" set. Words with several pronunciations keep their default one.
/// </summary>
[DebuggerDisplay("PronunciationLexicon: {Count} words")]
internal sealed class PronunciationLexicon
{
    /// <summary>The key holding a word's default pronunciation when it has several.</summary>
    private const string DefaultKey = "DEFAULT";

    /// <summary>The pronunciations.</summary>
    private readonly FrozenDictionary<string, string> _words;

    /// <summary>Looks words up from a span of characters, so no string is made to ask.</summary>
    private readonly FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    /// <summary>Initializes a new instance of the <see cref="PronunciationLexicon"/> class.</summary>
    /// <param name="words">The pronunciations.</param>
    private PronunciationLexicon(FrozenDictionary<string, string> words)
    {
        _words = words;
        _lookup = words.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>Gets the number of words.</summary>
    internal int Count => _words.Count;

    /// <summary>Reads the dictionaries; the first wins where both have a word.</summary>
    /// <param name="dictionaries">The JSON dictionaries, gold first.</param>
    /// <returns>The lexicon.</returns>
    internal static PronunciationLexicon Load(params ReadOnlySpan<byte[]> dictionaries)
    {
        var words = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var json in dictionaries)
        {
            Read(json, words);
        }

        Grow(words);
        return new(words.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>Looks a word up exactly.</summary>
    /// <param name="word">The word.</param>
    /// <param name="phonemes">The phonemes.</param>
    /// <returns><see langword="true"/> when found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGet(scoped ReadOnlySpan<char> word, out string phonemes) => _lookup.TryGetValue(word, out phonemes!);

    /// <summary>Reads one dictionary: an object of words to a pronunciation, or to pronunciations by part of speech.</summary>
    /// <param name="json">The UTF-8 JSON.</param>
    /// <param name="words">The words so far; existing entries are kept.</param>
    private static void Read(byte[] json, Dictionary<string, string> words)
    {
        var reader = new Utf8JsonReader(json, new() { CommentHandling = JsonCommentHandling.Skip });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var word = reader.GetString()!;
            _ = reader.Read();
            var phonemes = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.StartObject => ReadDefault(ref reader),
                _ => Skip(ref reader),
            };
            if (!string.IsNullOrEmpty(phonemes))
            {
                _ = words.TryAdd(word, phonemes);
            }
        }
    }

    /// <summary>Reads the default pronunciation from a by-part-of-speech object.</summary>
    /// <param name="reader">The reader, on the object's start.</param>
    /// <returns>The default pronunciation, or <see langword="null"/>.</returns>
    private static string? ReadDefault(ref Utf8JsonReader reader)
    {
        string? result = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isDefault = reader.ValueTextEquals(DefaultKey);
            _ = reader.Read();
            if (isDefault && reader.TokenType == JsonTokenType.String)
            {
                result = reader.GetString();
            }
        }

        return result;
    }

    /// <summary>Skips a value of an unexpected kind.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns><see langword="null"/>.</returns>
    private static string? Skip(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    /// <summary>Adds capitalised forms of lower case words and lower case forms of capitalised ones, as misaki does.</summary>
    /// <param name="words">The words.</param>
    private static void Grow(Dictionary<string, string> words)
    {
        var extra = new List<KeyValuePair<string, string>>();
        foreach (var (word, phonemes) in words)
        {
            if (word.Length < 2)
            {
                continue;
            }

            var lower = word.ToLowerInvariant();
            var capitalised = string.Concat(char.ToUpperInvariant(lower[0]).ToString(), lower.AsSpan(1));
            if (word == lower && word != capitalised)
            {
                extra.Add(new(capitalised, phonemes));
            }
            else if (word == capitalised)
            {
                extra.Add(new(lower, phonemes));
            }
        }

        foreach (var (word, phonemes) in extra)
        {
            _ = words.TryAdd(word, phonemes);
        }
    }
}
