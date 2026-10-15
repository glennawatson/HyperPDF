// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Spelling;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <content>Spell checking for the text field being edited, with the desktop's own dictionaries.</content>
public sealed partial class FormsViewModel
{
    /// <summary>The words the reader chose to keep, read on first use.</summary>
    private HashSet<string>? _keptWords;

    /// <summary>Gets a number that changes whenever the words kept as spelled change, so marks are found again.</summary>
    [Reactive]
    public partial int KeptWordsVersion { get; private set; }

    /// <summary>Gets a value indicating whether typed text is checked: the reader wants it and a dictionary was found.</summary>
    public bool ChecksSpelling => _owner.Services.Settings.CheckSpelling && _owner.Services.SpellChecker.IsAvailable;

    /// <summary>Finds the misspelled words in text typed into a field.</summary>
    /// <param name="text">The text.</param>
    /// <param name="output">Receives each misspelled word's place; cleared first.</param>
    public void FindMisspelled(string text, List<TextRange> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        if (string.IsNullOrEmpty(text) || !ChecksSpelling)
        {
            return;
        }

        SpellingWords.FindMisspelled(text, _owner.Services.SpellChecker, IgnoredWords(), output);
    }

    /// <summary>Gets corrections for the misspelled word at a place in the editor's text.</summary>
    /// <param name="index">A character index in <see cref="EditText"/>.</param>
    /// <param name="suggestions">Receives the corrections, after it is cleared; it stays empty when the word is spelled correctly or there is none.</param>
    /// <param name="word">The word found there, or an empty range.</param>
    public void SuggestAt(int index, List<string> suggestions, out TextRange word)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        suggestions.Clear();
        word = SpellingWords.WordAt(EditText, index);
        if (!ChecksSpelling || word.IsEmpty)
        {
            return;
        }

        var text = EditText.Substring(word.Start, word.Length);
        var checker = _owner.Services.SpellChecker;
        if (!checker.IsCorrect(text) && !IgnoredWords().Contains(text))
        {
            checker.Suggest(text, suggestions);
        }
    }

    /// <summary>Replaces a word in the editor's text with a correction.</summary>
    /// <param name="word">The word's place.</param>
    /// <param name="replacement">The correction.</param>
    public void ReplaceWord(TextRange word, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (word.Start < 0 || word.Start + word.Length > EditText.Length)
        {
            return;
        }

        EditText = string.Concat(EditText.AsSpan(0, word.Start), replacement, EditText.AsSpan(word.Start + word.Length));
    }

    /// <summary>Keeps a word as it is spelled, here and in every field from now on.</summary>
    /// <param name="word">The word.</param>
    public void IgnoreWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || IgnoredWords().Contains(word))
        {
            return;
        }

        _owner.Services.Settings.IgnoredWords.Add(word);
        _ = IgnoredWords().Add(word);
        _owner.Services.SaveSettings();
        KeptWordsVersion++;
    }

    /// <summary>Replaces a misspelled word with the chosen correction.</summary>
    /// <param name="fix">The word and its correction.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Correct(SpellingFix fix) => ReplaceWord(fix.Word, fix.Replacement);

    /// <summary>Keeps a word as it is spelled.</summary>
    /// <param name="word">The word.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KeepSpelling(string word) => IgnoreWord(word);

    /// <summary>Gets the words the reader chose to keep, read from the settings once.</summary>
    /// <returns>The words, compared without case.</returns>
    private HashSet<string> IgnoredWords() => _keptWords ??= new(_owner.Services.Settings.IgnoredWords, StringComparer.OrdinalIgnoreCase);
}
