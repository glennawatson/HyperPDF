// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// The tokenizer of bert-base-uncased: text is cleaned, lower-cased and stripped of accents, split at spaces and
/// punctuation, and each word is cut into the longest pieces the vocabulary holds, continuing pieces marked with
/// <c>##</c>. MeloTTS groups these pieces into words and feeds their ids to BERT, so this must match the original.
/// </summary>
[DebuggerDisplay("WordPieceTokenizer: {Count} pieces")]
internal sealed class WordPieceTokenizer
{
    /// <summary>The id of the token that opens every sentence.</summary>
    internal const int Start = 101;

    /// <summary>The id of the token that closes every sentence.</summary>
    internal const int End = 102;

    /// <summary>The id of a word the vocabulary cannot spell.</summary>
    internal const int Unknown = 100;

    /// <summary>The marker that starts a continuing piece.</summary>
    private const string Continuation = "##";

    /// <summary>The length of <see cref="Continuation"/>.</summary>
    private const int ContinuationLength = 2;

    /// <summary>The most characters one input character becomes when cleaned: a space each side of punctuation.</summary>
    private const int SpacedLength = 3;

    /// <summary>The longest word cut into pieces; longer words are unknown.</summary>
    private const int MaxWordLength = 100;

    /// <summary>The ids by piece.</summary>
    private readonly Dictionary<string, int> _ids;

    /// <summary>The ids by piece, looked up by span.</summary>
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    /// <summary>The pieces by id.</summary>
    private readonly string[] _pieces;

    /// <summary>The cleaned text, reused.</summary>
    private char[] _clean = [];

    /// <summary>Initializes a new instance of the <see cref="WordPieceTokenizer"/> class.</summary>
    /// <param name="vocabulary">The vocabulary: one piece per line, its line number its id.</param>
    internal WordPieceTokenizer(string vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        _pieces = vocabulary.Split('\n');
        if (_pieces.Length > 0 && _pieces[^1].Length == 0)
        {
            _pieces = _pieces[..^1];
        }

        _ids = [with(_pieces.Length, StringComparer.Ordinal)];
        for (var i = 0; i < _pieces.Length; i++)
        {
            _pieces[i] = _pieces[i].TrimEnd('\r');
            _ = _ids.TryAdd(_pieces[i], i);
        }

        _lookup = _ids.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>Gets the number of pieces in the vocabulary.</summary>
    internal int Count => _pieces.Length;

    /// <summary>Gets a piece's text.</summary>
    /// <param name="id">The piece's id.</param>
    /// <returns>The text, with <c>##</c> on continuing pieces.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string Piece(int id) => _pieces[id];

    /// <summary>Cuts text into piece ids, without the start and end tokens.</summary>
    /// <param name="text">The text.</param>
    /// <param name="ids">Receives the ids; cleared first.</param>
    internal void Tokenize(string text, List<int> ids)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ids);
        ids.Clear();
        var clean = Clean(text);
        foreach (var range in clean.Split(' '))
        {
            var word = clean[range];
            if (!word.IsEmpty)
            {
                AddWord(word, ids);
            }
        }
    }

    /// <summary>Determines whether a character is punctuation as BERT counts it: ASCII symbols and Unicode punctuation.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for punctuation.</returns>
    private static bool IsPunctuation(char c) =>
        c is > ' ' and < '\u007F' ? !char.IsAsciiLetterOrDigit(c) : char.IsPunctuation(c);

    /// <summary>Determines whether a character is a Chinese, Japanese or Korean ideograph, which BERT treats as a word.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for an ideograph.</returns>
    private static bool IsIdeograph(char c) =>
        c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿');

    /// <summary>Determines whether BERT drops a character: nulls, replacement characters, accents and control characters.</summary>
    /// <param name="c">The character, already known not to be white space.</param>
    /// <returns><see langword="true"/> when dropped.</returns>
    private static bool IsDropped(char c) =>
        c is '\0' or '\uFFFD'
        || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.Control or UnicodeCategory.Format;

    /// <summary>
    /// Cleans text as BERT does: lower-cases it, strips accents, drops control characters, turns white space into
    /// spaces, and puts spaces around punctuation and ideographs so each is a word of its own.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The cleaned text, in a reused buffer.</returns>
    private ReadOnlySpan<char> Clean(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        if (_clean.Length < decomposed.Length * SpacedLength)
        {
            _clean = new char[decomposed.Length * SpacedLength];
        }

        var length = 0;
        foreach (var c in decomposed)
        {
            if (char.IsWhiteSpace(c))
            {
                _clean[length] = ' ';
                length++;
            }
            else if (IsPunctuation(c) || IsIdeograph(c))
            {
                " _ ".CopyTo(_clean.AsSpan(length));
                _clean[length + 1] = c;
                length += SpacedLength;
            }
            else if (!IsDropped(c))
            {
                _clean[length] = c;
                length++;
            }
        }

        return _clean.AsSpan(0, length);
    }

    /// <summary>Cuts one word into the longest pieces the vocabulary holds; a word that cannot be cut is unknown.</summary>
    /// <param name="word">The word.</param>
    /// <param name="ids">Receives the ids.</param>
    private void AddWord(ReadOnlySpan<char> word, List<int> ids)
    {
        if (word.Length > MaxWordLength)
        {
            ids.Add(Unknown);
            return;
        }

        Span<char> candidate = stackalloc char[MaxWordLength + ContinuationLength];
        Continuation.CopyTo(candidate);
        var first = ids.Count;
        var position = 0;
        while (position < word.Length)
        {
            var found = FindPiece(word, position, candidate, out var end);
            if (found < 0)
            {
                ids.RemoveRange(first, ids.Count - first);
                ids.Add(Unknown);
                return;
            }

            ids.Add(found);
            position = end;
        }
    }

    /// <summary>Finds the longest piece the vocabulary holds at a position in a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="position">Where the piece starts.</param>
    /// <param name="candidate">Scratch space starting with the continuation marker.</param>
    /// <param name="end">Where the piece ends.</param>
    /// <returns>The piece's id, or -1 when none fits.</returns>
    private int FindPiece(ReadOnlySpan<char> word, int position, Span<char> candidate, out int end)
    {
        for (end = word.Length; end > position; end--)
        {
            var piece = word[position..end];
            if (position > 0)
            {
                piece.CopyTo(candidate[ContinuationLength..]);
                piece = candidate[..(ContinuationLength + piece.Length)];
            }

            if (_lookup.TryGetValue(piece, out var id))
            {
                return id;
            }
        }

        return -1;
    }
}
