// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Spelling;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Checks spelling against a plain word list the desktop installed, one word a line, such as
/// /usr/share/dict/british-english. Words are looked up in a set; corrections come from a <see cref="CorrectionIndex"/>
/// built only the first time a correction is asked for. No word list ships with the app.
/// </summary>
[DebuggerDisplay("WordListSpellChecker: {Language}")]
public sealed class WordListSpellChecker : ISpellChecker
{
    /// <summary>The longest word whose apostrophes are straightened on the stack.</summary>
    private const int MaxStackWord = 64;

    /// <summary>The most suggestions offered.</summary>
    private const int MaxSuggestions = 5;

    /// <summary>The capital letters, to tell a word written all in capitals.</summary>
    private static readonly System.Buffers.SearchValues<char> UpperCase = System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞ");

    /// <summary>The words, compared without case.</summary>
    private readonly HashSet<string> _words = [with(comparer: StringComparer.OrdinalIgnoreCase)];

    /// <summary>Guards building and using the correction index.</summary>
    private readonly Lock _gate = new();

    /// <summary>The correction index, built on first use.</summary>
    private CorrectionIndex? _index;

    /// <summary>Initializes a new instance of the <see cref="WordListSpellChecker"/> class from a word list file.</summary>
    /// <param name="wordListPath">The word list, or <see langword="null"/> when none was found.</param>
    public WordListSpellChecker(string? wordListPath)
    {
        if (wordListPath is null)
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(wordListPath))
            {
                var word = line.Trim();
                if (word.Length > 0 && !word.StartsWith('#'))
                {
                    _ = _words.Add(word);
                }
            }

            Language = Path.GetFileNameWithoutExtension(wordListPath);
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Could not read the word list {wordListPath}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceWarning($"Could not read the word list {wordListPath}: {ex.Message}");
        }
    }

    /// <summary>Gets the folders where Linux desktops install word lists.</summary>
    public static IReadOnlyList<string> SystemFolders { get; } = ["/usr/share/dict", "/usr/local/share/dict", "/run/host/usr/share/dict"];

    /// <summary>Gets the word list's name, such as british-english, or empty when there is none.</summary>
    public string Language { get; } = string.Empty;

    /// <inheritdoc/>
    public bool IsAvailable => _words.Count > 0;

    /// <summary>Finds the word list for a language, trying the names Linux packages use, and then the reader's own list.</summary>
    /// <param name="culture">The language.</param>
    /// <param name="folders">The folders to look in, in order.</param>
    /// <returns>The word list, or <see langword="null"/>.</returns>
    public static string? FindWordList(CultureInfo culture, IReadOnlyList<string> folders)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(folders);
        foreach (var name in WordListNames.For(culture))
        {
            foreach (var folder in folders)
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsCorrect(ReadOnlySpan<char> word)
    {
        if (_words.Count == 0)
        {
            return true;
        }

        var lookup = _words.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.Contains(word))
        {
            return true;
        }

        if (word.IndexOf('’') < 0 || word.Length > MaxStackWord)
        {
            return false;
        }

        // Typed curly apostrophes match the straight ones word lists use.
        Span<char> straight = stackalloc char[MaxStackWord];
        word.Replace(straight[..word.Length], '’', '\'');
        return lookup.Contains(straight[..word.Length]);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Suggest(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (_words.Count == 0)
        {
            return [];
        }

        if (word.Length is 0 or > CorrectionIndex.MaxWordLength)
        {
            return [];
        }

        Span<char> lower = stackalloc char[CorrectionIndex.MaxWordLength];
        lower = lower[..word.AsSpan().ToLowerInvariant(lower)];
        List<string> suggestions = [with(MaxSuggestions)];
        lock (_gate)
        {
            Index().Lookup(lower, suggestions, MaxSuggestions);
        }

        var corrections = CollectionsMarshal.AsSpan(suggestions);
        foreach (ref var correction in corrections)
        {
            correction = MatchCase(word, correction);
        }

        return suggestions;
    }

    /// <summary>Gives a correction the misspelling's capitals: a first capital, or all capitals.</summary>
    /// <param name="typed">The misspelling.</param>
    /// <param name="correction">The correction in lower case.</param>
    /// <returns>The correction as it should be written.</returns>
    private static string MatchCase(string typed, string correction)
    {
        if (typed.Length > 1 && !typed.AsSpan().ContainsAnyExcept(UpperCase))
        {
            return correction.ToUpperInvariant();
        }

        return char.IsUpper(typed[0]) ? string.Concat(correction[..1].ToUpperInvariant(), correction.AsSpan(1)) : correction;
    }

    /// <summary>Gets the correction index, building it from the words the first time. Callers hold the gate.</summary>
    /// <returns>The index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CorrectionIndex Index() => _index ??= new(_words);
}
