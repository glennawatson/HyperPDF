// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Core.Media;

/// <summary>Reads captions in the SubRip (<c>.srt</c>) format that <c>ffmpeg</c> writes.</summary>
public static class SrtCaptions
{
    /// <summary>The most captions kept, so a damaged or hostile file cannot fill memory.</summary>
    private const int MaxCues = 50_000;

    /// <summary>The fewest lines a caption takes: its timing, its words and the blank line that ends it.</summary>
    private const int MinimumCueLines = 3;

    /// <summary>Reads the captions of a file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The captions in the order the file lists them.</returns>
    public static List<CaptionCue> Parse(ReadOnlySpan<char> text)
    {
        // A caption takes at least three lines, and counting one character is a single vectorised pass, so the first buffer is sized for the most captions there can be.
        var reader = new CueReader(Math.Min((text.Count('\n') / MinimumCueLines) + 1, MaxCues));
        foreach (var line in text.EnumerateLines())
        {
            reader.Read(line.Trim());
        }

        reader.Close();
        return reader.ToList();
    }

    /// <summary>Finds the caption shown at a moment.</summary>
    /// <param name="cues">The captions, in the order the file lists them.</param>
    /// <param name="position">The moment.</param>
    /// <returns>The words, or an empty string when no caption is showing.</returns>
    public static string TextAt(List<CaptionCue> cues, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(cues);

        // Reading each cue in place avoids copying the 24 byte value out of the list at every step.
        foreach (ref readonly var cue in CollectionsMarshal.AsSpan(cues))
        {
            if (position >= cue.Start && position < cue.End)
            {
                return cue.Text;
            }
        }

        return string.Empty;
    }

    /// <summary>Collects captions one line at a time into a pooled buffer, so the list that is returned is exactly the size of the result.</summary>
    /// <param name="capacity">The captions the first buffer holds.</param>
    private sealed class CueReader(int capacity)
    {
        /// <summary>The longest caption kept, in characters.</summary>
        private const int MaxTextLength = 2_000;

        /// <summary>The factor by which a full buffer grows.</summary>
        private const int GrowthFactor = 2;

        /// <summary>The words of the caption being read.</summary>
        private readonly StringBuilder _words = new();

        /// <summary>The buffer the captions are collected in.</summary>
        private CaptionCue[] _cues = ArrayPool<CaptionCue>.Shared.Rent(capacity);

        /// <summary>The captions in the buffer.</summary>
        private int _count;

        /// <summary>Whether the words being added are inside a markup tag, which can run across lines.</summary>
        private bool _inTag;

        /// <summary>When the caption being read appears.</summary>
        private TimeSpan _start;

        /// <summary>When the caption being read goes away.</summary>
        private TimeSpan _end;

        /// <summary>Whether a caption is being read.</summary>
        private bool _open;

        /// <summary>Copies the captions into a list of exactly their number and gives the buffer back to the pool.</summary>
        /// <returns>The captions.</returns>
        internal List<CaptionCue> ToList()
        {
            List<CaptionCue> list = [with(_count)];
            list.AddRange(new ReadOnlySpan<CaptionCue>(_cues, 0, _count));
            ArrayPool<CaptionCue>.Shared.Return(_cues, true);
            _cues = [];
            return list;
        }

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
            if (_open && _words.Length > 0 && _count < MaxCues)
            {
                if (_count == _cues.Length)
                {
                    Grow();
                }

                _cues[_count] = new(_start, _end, _words.ToString());
                _count++;
            }

            _ = _words.Clear();
            _open = false;
            _inTag = false;
        }

        /// <summary>Moves the captions to a larger buffer, for files that have fewer lines to a caption than the first guess allowed for.</summary>
        private void Grow()
        {
            var larger = ArrayPool<CaptionCue>.Shared.Rent(_cues.Length * GrowthFactor);
            _cues.AsSpan(0, _count).CopyTo(larger);
            ArrayPool<CaptionCue>.Shared.Return(_cues, true);
            _cues = larger;
        }

        /// <summary>Adds a line of a caption without the markup tags some caption files use for italics and colours, keeping the length bounded.</summary>
        /// <param name="line">The line.</param>
        private void AddWords(ReadOnlySpan<char> line)
        {
            if (_words.Length > 0 && _words.Length < MaxTextLength)
            {
                _ = _words.Append('\n');
            }

            // A tag runs from < to the next >, across lines if it must, and the text between tags is kept a stretch at a time.
            while (!line.IsEmpty)
            {
                if (_inTag)
                {
                    var close = line.IndexOf('>');
                    if (close < 0)
                    {
                        return;
                    }

                    _inTag = false;
                    line = line[(close + 1)..];
                    continue;
                }

                var open = line.IndexOf('<');
                Keep(open < 0 ? line : line[..open]);
                if (open < 0)
                {
                    return;
                }

                _inTag = true;
                line = line[(open + 1)..];
            }
        }

        /// <summary>Adds words outside any tag, as many as fit under the length limit.</summary>
        /// <param name="text">The words.</param>
        private void Keep(ReadOnlySpan<char> text)
        {
            var room = MaxTextLength - _words.Length;
            if (room > 0)
            {
                _ = _words.Append(text[..Math.Min(room, text.Length)]);
            }
        }
    }
}
