// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Media;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests that <see cref="SrtCaptions.Parse"/> keeps the words of a caption exactly as a character by character reader does, whatever tags and lengths the text has.</summary>
public sealed class SrtCaptionsReferenceTests
{
    /// <summary>The starting state of the number sequence.</summary>
    private const uint Seed = 2_463_534_242;

    /// <summary>The files compared.</summary>
    private const int Files = 300;

    /// <summary>The captions in the file that has two lines to each.</summary>
    private const int ShortCues = 300;

    /// <summary>The captions in each file.</summary>
    private const int CuesPerFile = 12;

    /// <summary>The alphabet the words are drawn from, weighted toward tags.</summary>
    private const string Alphabet = "abc xyz<<>>/i <i>é";

    /// <summary>The most lines a caption may have.</summary>
    private const int MostLines = 4;

    /// <summary>The longest ordinary line.</summary>
    private const int MostCharacters = 60;

    /// <summary>The chance, out of this many, that a line is far longer than the limit.</summary>
    private const int LongLineOdds = 25;

    /// <summary>The length of a line that is far longer than the limit.</summary>
    private const int LongLine = 2500;

    /// <summary>The seconds shown in the timing lines before the digits wrap.</summary>
    private const int SecondsWrap = 10;

    /// <summary>Verifies the captions read match the reference reader's for generated text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParseMatchesTheCharacterByCharacterReader()
    {
        var numbers = new NumberSequence(Seed);
        var differences = 0;
        for (var file = 0; file < Files; file++)
        {
            var text = Build(numbers);
            var actual = SrtCaptions.Parse(text);
            var expected = Reference(text);
            differences += actual.SequenceEqual(expected) ? 0 : 1;
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>Verifies a file with only a timing line and a line of words for each caption, which has fewer lines than the first guess at the number of captions allows for, is read whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilesWithFewLinesToACaptionAreReadWhole()
    {
        var text = new StringBuilder();
        for (var cue = 0; cue < ShortCues; cue++)
        {
            _ = text.Append("00:00:01,000 --> 00:00:02,000\nwords ").Append(cue).Append('\n');
        }

        var actual = SrtCaptions.Parse(text.ToString());

        await Assert.That(actual.Count).IsEqualTo(ShortCues);
        await Assert.That(actual.SequenceEqual(Reference(text.ToString()))).IsTrue();
    }

    /// <summary>Builds a caption file with words, tags and line lengths drawn from the sequence.</summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The text.</returns>
    private static string Build(NumberSequence numbers)
    {
        var text = new StringBuilder();
        for (var cue = 0; cue < CuesPerFile; cue++)
        {
            var seconds = cue % SecondsWrap;
            _ = text.Append(cue + 1).Append("\n00:00:0").Append(seconds).Append(",000 --> 00:00:0").Append(seconds).Append(",900\n");
            var lines = numbers.Next(MostLines);
            for (var line = 0; line < lines; line++)
            {
                AppendLine(text, numbers);
            }

            _ = text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Appends one line of words, now and then a very long one.</summary>
    /// <param name="text">The file being built.</param>
    /// <param name="numbers">The numbers.</param>
    private static void AppendLine(StringBuilder text, NumberSequence numbers)
    {
        var length = numbers.Next(LongLineOdds) == 0 ? LongLine : numbers.Next(MostCharacters);
        for (var i = 0; i < length; i++)
        {
            _ = text.Append(Alphabet[numbers.Next(Alphabet.Length)]);
        }

        _ = text.Append('\n');
    }

    /// <summary>Reads captions one character at a time, as the reader did before it kept stretches of words.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The captions.</returns>
    private static List<CaptionCue> Reference(string text)
    {
        var reader = new ReferenceReader();
        foreach (var line in text.AsSpan().EnumerateLines())
        {
            reader.Read(line.Trim());
        }

        reader.Close();
        return reader.Cues;
    }

    /// <summary>The reader as it was before it kept stretches of words, which adds each character on its own.</summary>
    private sealed class ReferenceReader
    {
        /// <summary>The longest caption kept, in characters.</summary>
        private const int MaxTextLength = 2000;

        /// <summary>The most captions kept.</summary>
        private const int MaxCues = 50_000;

        /// <summary>The words of the caption being read.</summary>
        private readonly StringBuilder _words = new();

        /// <summary>Whether the words being added are inside a markup tag.</summary>
        private bool _inTag;

        /// <summary>When the caption being read appears.</summary>
        private TimeSpan _start;

        /// <summary>When the caption being read goes away.</summary>
        private TimeSpan _end;

        /// <summary>Whether a caption is being read.</summary>
        private bool _open;

        /// <summary>Gets the captions read so far.</summary>
        internal List<CaptionCue> Cues { get; } = [];

        /// <summary>Reads one trimmed line.</summary>
        /// <param name="line">The line.</param>
        internal void Read(ReadOnlySpan<char> line)
        {
            if (SrtTimes.TryReadTimes(line, out var from, out var to))
            {
                Close();
                _start = from;
                _end = to;
                _open = true;
            }
            else if (line.IsEmpty)
            {
                Close();
            }
            else if (_open)
            {
                AddWords(line);
            }
        }

        /// <summary>Adds the caption being read, if it has words, and starts a new one.</summary>
        internal void Close()
        {
            if (_open && _words.Length > 0 && Cues.Count < MaxCues)
            {
                Cues.Add(new(_start, _end, _words.ToString()));
            }

            _ = _words.Clear();
            _open = false;
            _inTag = false;
        }

        /// <summary>Adds a line of a caption without the markup tags, keeping the length bounded.</summary>
        /// <param name="line">The line.</param>
        private void AddWords(ReadOnlySpan<char> line)
        {
            if (_words.Length is > 0 and < MaxTextLength)
            {
                _ = _words.Append('\n');
            }

            foreach (var letter in line)
            {
                if (letter == '<')
                {
                    _inTag = true;
                }
                else if (letter == '>' && _inTag)
                {
                    _inTag = false;
                }
                else if (!_inTag && _words.Length < MaxTextLength)
                {
                    _ = _words.Append(letter);
                }
            }
        }
    }
}
