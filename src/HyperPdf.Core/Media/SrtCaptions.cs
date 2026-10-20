// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Media;

/// <summary>Reads captions in the SubRip (<c>.srt</c>) format that <c>ffmpeg</c> writes.</summary>
public static class SrtCaptions
{
    /// <summary>Reads the captions of a file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The captions in the order the file lists them.</returns>
    public static List<CaptionCue> Parse(ReadOnlySpan<char> text)
    {
        var reader = new CueReader();
        foreach (var line in text.EnumerateLines())
        {
            reader.Read(line.Trim());
        }

        reader.Close();
        return reader.Cues;
    }

    /// <summary>Finds the caption shown at a moment.</summary>
    /// <param name="cues">The captions, in the order the file lists them.</param>
    /// <param name="position">The moment.</param>
    /// <returns>The words, or an empty string when no caption is showing.</returns>
    public static string TextAt(List<CaptionCue> cues, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(cues);
        foreach (var cue in cues)
        {
            if (position >= cue.Start && position < cue.End)
            {
                return cue.Text;
            }
        }

        return string.Empty;
    }

    /// <summary>Collects captions one line at a time.</summary>
    private sealed class CueReader
    {
        /// <summary>The most captions kept, so a damaged or hostile file cannot fill memory.</summary>
        private const int MaxCues = 50_000;

        /// <summary>The longest caption kept, in characters.</summary>
        private const int MaxTextLength = 2_000;

        /// <summary>The words of the caption being read.</summary>
        private readonly StringBuilder _words = new();

        /// <summary>Whether the words being added are inside a markup tag, which can run across lines.</summary>
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

        /// <summary>Adds a line of a caption without the markup tags some caption files use for italics and colours, keeping the length bounded.</summary>
        /// <param name="line">The line.</param>
        private void AddWords(ReadOnlySpan<char> line)
        {
            if (_words.Length > 0 && _words.Length < MaxTextLength)
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
