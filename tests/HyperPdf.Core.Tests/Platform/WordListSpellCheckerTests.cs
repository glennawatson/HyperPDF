// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Platform.Linux.Spelling;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests the word list spell checker against small lists written for the test.</summary>
public sealed class WordListSpellCheckerTests
{
    /// <summary>The British English list's name.</summary>
    private const string British = "british-english";

    /// <summary>The words in each list.</summary>
    private const string Words = "hello\nworld\nreceive\nrelieve\nform\nBritain\n";

    /// <summary>Verifies words are checked without case, and corrections keep the typed capitals.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksAndSuggests()
    {
        var folder = CreateWordLists(British);
        try
        {
            var checker = new WordListSpellChecker(Path.Combine(folder, British));

            await Assert.That(checker.IsAvailable).IsTrue();
            await Assert.That(checker.Language).IsEqualTo(British);
            await Assert.That(checker.IsCorrect("Receive")).IsTrue();
            await Assert.That(checker.IsCorrect("britain")).IsTrue();
            await Assert.That(checker.IsCorrect("recieve")).IsFalse();
            await Assert.That(checker.Suggest("recieve")).Contains("receive");
            await Assert.That(checker.Suggest("Recieve")).Contains("Receive");
            await Assert.That(checker.Suggest("RECIEVE")).Contains("RECEIVE");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Verifies the reader's own list comes first, then the package for their region, and none means unavailable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsTheReadersWordList()
    {
        var folder = CreateWordLists("american-english", British, "en-GB.txt", "french");
        try
        {
            string[] folders = [Path.Combine(folder, "missing"), folder];

            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("en-GB"), folders))).IsEqualTo("en-GB.txt");
            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("en-AU"), folders))).IsEqualTo(British);
            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("fr-CA"), folders))).IsEqualTo("french");
            await Assert.That(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("ja-JP"), folders)).IsNull();
            await Assert.That(new WordListSpellChecker(null).IsAvailable).IsFalse();
            await Assert.That(new WordListSpellChecker(null).IsCorrect("anything")).IsTrue();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Writes word lists into a new folder.</summary>
    /// <param name="names">The lists' file names.</param>
    /// <returns>The folder.</returns>
    private static string CreateWordLists(params string[] names)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-words-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(folder, name), Words);
        }

        return folder;
    }
}
