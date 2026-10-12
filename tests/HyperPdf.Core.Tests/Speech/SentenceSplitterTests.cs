// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Tests for <see cref="SentenceSplitter"/>.</summary>
public sealed class SentenceSplitterTests
{
    /// <summary>The words in a long run with no full stop.</summary>
    private const int Words = 180;

    /// <summary>The sentences in the paragraph test.</summary>
    private const int ParagraphCount = 2;

    /// <summary>The longest piece spoken at once.</summary>
    private const int MaxLength = 300;

    /// <summary>Verifies sentences end at full stops, question and exclamation marks, but not at abbreviations or initials.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitsSentences()
    {
        const string text = "Dr. Smith met J. Brown at 3 p.m. today. Was it late? Yes!  It was.";
        List<SpeechSentence> sentences = [];
        SentenceSplitter.Split(text, sentences);
        var spoken = sentences.ConvertAll(static s => text.Substring(s.Start, s.Length));

        await Assert.That(spoken).IsEquivalentTo(["Dr. Smith met J. Brown at 3 p.m. today.", "Was it late?", "Yes!", "It was."]);
    }

    /// <summary>Verifies a blank line ends a sentence and punctuation-only runs are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitsParagraphs()
    {
        const string text = "Heading\r\n\r\nBody text here\n\n— . —";
        List<SpeechSentence> sentences = [];
        SentenceSplitter.Split(text, sentences);

        await Assert.That(sentences.Count).IsEqualTo(ParagraphCount);
        await Assert.That(text.Substring(sentences[0].Start, sentences[0].Length)).IsEqualTo("Heading");
    }

    /// <summary>Verifies a heading on its own line is read on its own, while a wrapped sentence stays whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitsHeadings()
    {
        const string text = "Page 1\r\nThe quick brown fox jumps over the lazy dog, then runs\r\nacross the field.\r\nUnique marker\r\nWith more";
        List<SpeechSentence> sentences = [];
        SentenceSplitter.Split(text, sentences);
        var spoken = sentences.ConvertAll(static s => SentenceSplitter.ToSpeech(text.AsSpan(s.Start, s.Length)));

        await Assert.That(spoken).IsEquivalentTo(["Page 1", "The quick brown fox jumps over the lazy dog, then runs across the field.", "Unique marker", "With more"]);
    }

    /// <summary>Verifies long runs are split so the voice never waits long.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitsLongRuns()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", Words));
        List<SpeechSentence> sentences = [];
        SentenceSplitter.Split(text, sentences);

        await Assert.That(sentences.Count).IsGreaterThan(1);
        await Assert.That(sentences.TrueForAll(static s => s.Length <= MaxLength)).IsTrue();
    }

    /// <summary>Verifies words hyphenated across lines are joined and line breaks become spaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparesSpeech() =>
        await Assert.That(SentenceSplitter.ToSpeech("The docu-\r\nment is\r\nhere.")).IsEqualTo("The document is here.");
}
