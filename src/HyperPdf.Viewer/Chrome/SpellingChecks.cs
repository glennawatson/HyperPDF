// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdf.ViewModels.FormFilling;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Checks the spelling of the word at the end of a text a person is typing, and carries out the correction they pick.</summary>
internal static class SpellingChecks
{
    /// <summary>Checks the word at the end of a text.</summary>
    /// <param name="forms">The form of the tab shown, which knows the spell checker and the words kept.</param>
    /// <param name="text">The text.</param>
    /// <returns>What was found, which has no corrections when the word is spelled right or spelling is not checked.</returns>
    internal static SpellingCheck Of(FormsViewModel forms, string text)
    {
        if (text.Length == 0 || !forms.ChecksSpelling)
        {
            return SpellingCheck.None with { Text = text };
        }

        var misspelled = new List<TextRange>();
        var suggestions = new List<string>();
        forms.FindMisspelled(text, misspelled);
        forms.SuggestIn(text, text.Length - 1, suggestions, out var word);
        if (word.IsEmpty || !misspelled.Contains(word))
        {
            return SpellingCheck.None with { Text = text, Misspelled = [.. misspelled] };
        }

        var labels = new List<string>(suggestions) { UiText.Format("Keep \"{0}\" as it is", text.Substring(word.Start, word.Length)) };
        return new(text, word, [.. suggestions], [.. labels], [.. misspelled]);
    }

    /// <summary>Does one of the corrections: replaces the word, or keeps it as it is.</summary>
    /// <param name="forms">The form of the tab shown.</param>
    /// <param name="check">What was found.</param>
    /// <param name="index">The place of the correction in <see cref="SpellingCheck.Labels"/>.</param>
    /// <param name="write">Replaces the text that was checked.</param>
    internal static void Correct(FormsViewModel forms, SpellingCheck check, int index, Action<string> write)
    {
        if (check.Word.IsEmpty || (uint)index >= (uint)check.Labels.Length)
        {
            return;
        }

        if (index < check.Suggestions.Length)
        {
            write(string.Concat(check.Text.AsSpan(0, check.Word.Start), check.Suggestions[index], check.Text.AsSpan(check.Word.Start + check.Word.Length)));
            return;
        }

        forms.IgnoreWord(check.Text.Substring(check.Word.Start, check.Word.Length));
    }
}
