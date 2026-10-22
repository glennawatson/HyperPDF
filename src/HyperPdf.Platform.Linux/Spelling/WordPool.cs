// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Text.Unicode;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Holds a word list as one pooled run of characters, with each word's start and a hash table for checking words
/// without case. No string is made per word. The pooled arrays go back to <see cref="ArrayPool{T}.Shared"/> on dispose.
/// </summary>
[DebuggerDisplay("WordPool: {Count} words")]
internal sealed class WordPool : IDisposable
{
    /// <summary>The largest word list file read, in bytes.</summary>
    internal const int MaxFileBytes = 256 * 1024 * 1024;

    /// <summary>The disposed bit of the check count.</summary>
    private const int Closed = int.MinValue;

    /// <summary>The comment marker that starts a line to skip.</summary>
    private const char Comment = '#';

    /// <summary>The slots per word in the hash table, which keeps probes short.</summary>
    private const int SlotsPerWord = 2;

    /// <summary>The fewest slots in the hash table.</summary>
    private const int MinimumSlots = 16;

    /// <summary>The characters of every word, one after another.</summary>
    private readonly char[] _chars;

    /// <summary>Where each word starts in <see cref="_chars"/>; one longer than the words, so the last word has an end.</summary>
    private readonly int[] _starts;

    /// <summary>The hash table of word numbers plus one, zero for an empty slot.</summary>
    private readonly int[] _table;

    /// <summary>The number of table slots, a power of two.</summary>
    private readonly int _slots;

    /// <summary>The checks running, with the <see cref="Closed"/> bit set once the pool is disposed.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="WordPool"/> class from text read into a pooled array.</summary>
    /// <param name="chars">A pooled array holding the text; the pool takes it over and rewrites it in place.</param>
    /// <param name="length">The characters of text.</param>
    private WordPool(char[] chars, int length)
    {
        _chars = chars;
        var text = chars.AsSpan(0, length);

        // Every line ends in a line break except possibly the last, so this many words is the most there can be.
        var bound = text.Count('\n') + text.Count('\r') + 1;
        _slots = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(bound * SlotsPerWord, MinimumSlots));
        _starts = ArrayPool<int>.Shared.Rent(bound + 1);
        _table = ArrayPool<int>.Shared.Rent(_slots);
        _table.AsSpan(0, _slots).Clear();
        Count = Parse(text);
    }

    /// <summary>Gets the number of distinct words, without regard to case.</summary>
    internal int Count { get; }

    /// <summary>Gets the byte order mark of UTF-8, which word lists may start with.</summary>
    private static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <inheritdoc/>
    public void Dispose()
    {
        var state = Volatile.Read(ref _state);
        while (state >= 0)
        {
            var seen = Interlocked.CompareExchange(ref _state, state | Closed, state);
            if (seen == state)
            {
                // With no check running the arrays go back now; otherwise the last check to leave returns them.
                if (state == 0)
                {
                    ReturnArrays();
                }

                return;
            }

            state = seen;
        }
    }

    /// <summary>Reads a word list file, one word to a line, skipping blank and comment lines and repeated words.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The words.</returns>
    /// <exception cref="IOException">The file could not be read or is too large.</exception>
    internal static WordPool Read(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
        var length = RandomAccess.GetLength(handle);
        if (length > MaxFileBytes)
        {
            throw new IOException("The word list is too large.");
        }

        var bytes = ArrayPool<byte>.Shared.Rent((int)length);
        char[]? chars = null;
        try
        {
            var filled = 0;
            while (filled < length)
            {
                var read = RandomAccess.Read(handle, bytes.AsSpan(filled, (int)length - filled), filled);
                if (read == 0)
                {
                    break;
                }

                filled += read;
            }

            var content = bytes.AsSpan(0, filled);
            if (content.StartsWith(ByteOrderMark))
            {
                content = content[ByteOrderMark.Length..];
            }

            // Each UTF-8 byte gives at most one UTF-16 unit.
            chars = ArrayPool<char>.Shared.Rent(Math.Max(content.Length, 1));
            _ = Utf8.ToUtf16(content, chars, out _, out var written);
            var pool = new WordPool(chars, written);
            chars = null;
            return pool;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            if (chars is not null)
            {
                ArrayPool<char>.Shared.Return(chars);
            }
        }
    }

    /// <summary>Makes a pool from words, as if they were the lines of a word list.</summary>
    /// <param name="words">The words.</param>
    /// <returns>The pool.</returns>
    internal static WordPool Create(ReadOnlySpan<string> words)
    {
        var length = 0;
        foreach (var word in words)
        {
            length += word.Length + 1;
        }

        var chars = ArrayPool<char>.Shared.Rent(Math.Max(length, 1));
        var at = 0;
        foreach (var word in words)
        {
            word.CopyTo(chars.AsSpan(at));
            at += word.Length;
            chars[at] = '\n';
            at++;
        }

        return new(chars, length);
    }

    /// <summary>Gets a word as it was written in the list.</summary>
    /// <param name="number">The word's number, below <see cref="Count"/>.</param>
    /// <returns>The word.</returns>
    internal ReadOnlySpan<char> Word(int number)
    {
        var starts = _starts;
        return _chars.AsSpan(starts[number], starts[number + 1] - starts[number]);
    }

    /// <summary>Determines whether a word is in the list, without regard to case.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal bool Contains(ReadOnlySpan<char> word)
    {
        // The arrays go back to the pool only after the last check that entered has left.
        if (!TryEnter())
        {
            return false;
        }

        try
        {
            return Find(_table, _slots, word) >= 0;
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>Registers a check, unless the pool was disposed.</summary>
    /// <returns><see langword="true"/> when the arrays stay valid until <see cref="Exit"/>.</returns>
    private bool TryEnter()
    {
        var state = Volatile.Read(ref _state);
        while (state >= 0)
        {
            var seen = Interlocked.CompareExchange(ref _state, state + 1, state);
            if (seen == state)
            {
                return true;
            }

            state = seen;
        }

        return false;
    }

    /// <summary>Ends a check, and returns the arrays when the pool was disposed meanwhile and this was the last check.</summary>
    private void Exit()
    {
        if (Interlocked.Decrement(ref _state) == Closed)
        {
            ReturnArrays();
        }
    }

    /// <summary>Gives the arrays back to the shared pools.</summary>
    private void ReturnArrays()
    {
        ArrayPool<char>.Shared.Return(_chars);
        ArrayPool<int>.Shared.Return(_starts);
        ArrayPool<int>.Shared.Return(_table);
    }

    /// <summary>Finds a word in the hash table by comparing its characters without case.</summary>
    /// <param name="table">The table.</param>
    /// <param name="slots">The slots, a power of two.</param>
    /// <param name="word">The word.</param>
    /// <returns>The slot holding the word, or the bitwise complement of the empty slot it would take.</returns>
    private int Find(int[] table, int slots, ReadOnlySpan<char> word)
    {
        var mask = slots - 1;
        var slot = string.GetHashCode(word, StringComparison.OrdinalIgnoreCase) & mask;
        var entry = table[slot];
        while (entry != 0)
        {
            if (Word(entry - 1).Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }

            slot = (slot + 1) & mask;
            entry = table[slot];
        }

        return ~slot;
    }

    /// <summary>Moves each distinct word of the text to the front of the same array and records it.</summary>
    /// <param name="text">The text, rewritten in place; a word is only ever moved to an earlier place.</param>
    /// <returns>The number of words kept.</returns>
    private int Parse(Span<char> text)
    {
        var starts = _starts;
        var table = _table;
        var count = 0;
        var write = 0;
        var read = 0;
        starts[0] = 0;
        while (read < text.Length)
        {
            var lineLength = text[read..].IndexOfAny('\r', '\n');
            var end = lineLength < 0 ? text.Length : read + lineLength;
            var line = text[read..end].Trim();
            read = end + 1;
            if (line.IsEmpty || line[0] == Comment)
            {
                continue;
            }

            var found = Find(table, _slots, line);
            if (found >= 0)
            {
                continue;
            }

            line.CopyTo(text[write..]);
            write += line.Length;
            count++;
            table[~found] = count;
            starts[count] = write;
        }

        return count;
    }
}
